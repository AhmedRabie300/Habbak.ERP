using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Stage 3 documents outside POS: sales, purchase returns and inventory, each posting through its
/// standard template (Docs/Posting-Engine-Implementation-Plan.md, section 13), plus the settings
/// endpoints the screen builds on.
/// </summary>
public class DocumentPostingTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string TemplatesUrl = "/api/v1/accounting/posting-templates";

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<(CycleIds Ids, PostingAccounts Accounts, HttpClient Client)> ArrangeAsync(params CompanyAccountRole[] leaveUnmapped)
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var accounts = await PostingSeed.MapAllRolesAsync(factory, ids, leaveUnmapped);
        return (ids, accounts, CreateClient(companyId));
    }

    private async Task<long> SeedCustomerAsync(CycleIds ids)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var customer = new Customer
        {
            CompanyId = ids.CompanyId, Code = $"CU-{Guid.NewGuid():N}"[..12], NameAr = "كافيه الجيران", NameEn = "Neighbour Cafe",
            CustomerType = CustomerType.Corporate, CreditLimit = 100000m, PaymentTermDays = 30
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    /// <summary>1 000 g of espresso at 1.20 = 1 200, 14% VAT = 168, total 1 368.</summary>
    private async Task<long> SeedSalesInvoiceAsync(CycleIds ids, long customerId, SalesInvoicePaymentType paymentType = SalesInvoicePaymentType.Credit)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var invoice = new SalesInvoice
        {
            CompanyId = ids.CompanyId, BranchId = ids.BranchId, CustomerId = customerId,
            InvoiceNumber = $"SI-{Random.Shared.Next(100000, 999999)}", InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentType = paymentType, Status = SalesInvoiceStatus.Draft,
            Subtotal = 1200m, TaxAmount = 168m, TotalAmount = 1368m
        };
        invoice.Lines.Add(new SalesInvoiceLine { LineNumber = 1, ItemId = ids.EspressoItemId, Quantity = 1000m, UnitPrice = 1.20m, LineTotal = 1200m });
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task<long?> EntryOfAsync(long companyId, Func<Habbak.ERP.Infrastructure.Persistence.AppDbContext, Task<long?>> read)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        return await read(db);
    }

    // ------------------------------------------------------------------ settings endpoints

    private sealed record Template(long Id, string NameAr, string TriggerType, int ExecutionOrder, bool IsActive);
    private sealed record Resolver(string Key, string Kind, string RequiredField);
    private sealed record Field(string Name, string Kind, string EntityType, bool IsContext, List<string>? Choices);
    private sealed record Screen(string ScreenCode, bool IsPosting, bool CanMoveStock, List<Field> Fields, List<Resolver> Resolvers, List<Template> Templates);

    [Fact]
    public async Task Screens_list_every_posting_screen_with_only_the_resolvers_it_can_feed()
    {
        var (_, _, client) = await ArrangeAsync();

        var screens = await client.GetFromJsonAsync<List<Screen>>($"{TemplatesUrl}/screens");

        // 15 + the five of the fixed assets & maintenance module.
        Assert.Equal(20, screens!.Count);
        var sales = screens.Single(s => s.ScreenCode == "SALES_INVOICE");
        Assert.Contains(sales.Resolvers, r => r.Key == "Customer.ReceivableAccountId");
        Assert.DoesNotContain(sales.Resolvers, r => r.Key == "Supplier.PayableAccountId");
        Assert.Equal(["Cash", "Credit"], sales.Fields.Single(f => f.Name == "PaymentType").Choices);
        Assert.False(sales.CanMoveStock);

        // The POS cashier is a value the module supplies from the shift, not an invoice field.
        var pos = screens.Single(s => s.ScreenCode == "POS_INVOICES");
        var cashier = pos.Fields.Single(f => f.Name == "CashierUserId");
        Assert.True(cashier.IsContext);
        Assert.Equal("Cashier", cashier.EntityType);
    }

    [Fact]
    public async Task Standard_templates_start_switched_off_and_the_screen_switch_turns_them_all_on()
    {
        var (_, _, client) = await ArrangeAsync();

        var created = await client.PostAsJsonAsync($"{TemplatesUrl}/defaults", new { screenCode = "POS_INVOICES" });
        created.EnsureSuccessStatusCode();

        var pos = (await client.GetFromJsonAsync<List<Screen>>($"{TemplatesUrl}/screens"))!.Single(s => s.ScreenCode == "POS_INVOICES");
        Assert.Equal(["Always", "HasStockMovement"], pos.Templates.Select(t => t.TriggerType));   // sales, then cost of sales
        Assert.False(pos.IsPosting);
        Assert.All(pos.Templates, t => Assert.False(t.IsActive));

        // Pressing it again would double every entry.
        var again = await client.PostAsJsonAsync($"{TemplatesUrl}/defaults", new { screenCode = "POS_INVOICES" });
        Assert.Contains("POST-TEMPLATE-DEFAULTS-EXIST", await again.Content.ReadAsStringAsync());

        (await client.PostAsJsonAsync($"{TemplatesUrl}/screens/POS_INVOICES/active", new { isActive = true })).EnsureSuccessStatusCode();
        pos = (await client.GetFromJsonAsync<List<Screen>>($"{TemplatesUrl}/screens"))!.Single(s => s.ScreenCode == "POS_INVOICES");
        Assert.True(pos.IsPosting);
        Assert.All(pos.Templates, t => Assert.True(t.IsActive));
    }

    private sealed record PreviewLine(int LineNumber, string Direction, string AccountDisplay, decimal Amount, string? AmountFormula, bool Skipped);
    private sealed record Preview(bool TriggerMatched, List<PreviewLine> Lines, decimal TotalDebit, decimal TotalCredit, bool IsBalanced, List<string> Problems);
    private sealed record ScreenPreviewItem(string NameAr, bool IsActive, Preview Preview);

    [Fact]
    public async Task Preview_shows_the_sample_entry_and_flags_an_unmapped_role()
    {
        var (_, _, client) = await ArrangeAsync(CompanyAccountRole.VatPayable);

        var response = await client.PostAsJsonAsync($"{TemplatesUrl}/preview", new
        {
            screenCode = "SALES_INVOICE",
            definition = new
            {
                nameAr = "بيع", nameEn = "Sale",
                lines = new object[]
                {
                    new { lineNumber = 1, direction = "Debit", accountSourceType = "Resolver", accountResolverKey = "Customer.ReceivableAccountId",
                          amountFormulaType = "DirectField", amountFieldName = "TotalAmount", conditionType = "None", costCenters = Array.Empty<object>() },
                    new { lineNumber = 2, direction = "Credit", accountSourceType = "FromCompany", accountResolverKey = "SalesRevenue",
                          amountFormulaType = "DirectField", amountFieldName = "NetAmount", conditionType = "None", costCenters = Array.Empty<object>() },
                    new { lineNumber = 3, direction = "Credit", accountSourceType = "FromCompany", accountResolverKey = "VatPayable",
                          amountFormulaType = "DirectField", amountFieldName = "TaxAmount", conditionType = "FieldGreaterThanZero",
                          conditionFieldName = "TaxAmount", costCenters = Array.Empty<object>() }
                }
            },
            sample = new { values = new Dictionary<string, decimal> { ["TotalAmount"] = 114m, ["NetAmount"] = 100m, ["TaxAmount"] = 14m } }
        });
        response.EnsureSuccessStatusCode();
        var preview = await response.Content.ReadFromJsonAsync<Preview>();

        Assert.True(preview!.IsBalanced);
        Assert.Equal(114m, preview.TotalDebit);
        Assert.Contains(preview.Problems, p => p.Contains("VatPayable"));
    }

    [Fact]
    public async Task A_template_naming_a_field_the_screen_does_not_have_is_refused()
    {
        var (ids, accounts, client) = await ArrangeAsync();

        var response = await client.PostAsJsonAsync(TemplatesUrl, new
        {
            screenCode = "SALES_INVOICE",
            definition = new
            {
                nameAr = "بيع", nameEn = "Sale",
                lines = new object[]
                {
                    new { lineNumber = 1, direction = "Debit", accountSourceType = "Fixed", fixedAccountId = accounts[CompanyAccountRole.Cash],
                          amountFormulaType = "DirectField", amountFieldName = "Totl", conditionType = "None", costCenters = Array.Empty<object>() },
                    new { lineNumber = 2, direction = "Credit", accountSourceType = "Fixed", fixedAccountId = accounts[CompanyAccountRole.SalesRevenue],
                          amountFormulaType = "DirectField", amountFieldName = "NetAmount", conditionType = "None", costCenters = Array.Empty<object>() }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("POST-TEMPLATE-UNKNOWN-FIELD", await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------------------ sales

    [Fact]
    public async Task A_sales_invoice_debits_the_customer_and_credits_revenue_and_vat()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "SALES_INVOICE");
        var customerId = await SeedCustomerAsync(ids);
        var invoiceId = await SeedSalesInvoiceAsync(ids, customerId);

        (await client.PostAsync($"/api/v1/sales/invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        var entryId = await EntryOfAsync(ids.CompanyId, db => db.SalesInvoices.Where(i => i.Id == invoiceId).Select(i => i.JournalEntryId).SingleAsync());
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, entryId!.Value);
        Assert.Equal(1368m, lines.Debit(accounts[CompanyAccountRole.DefaultReceivable])); // customer has no own account
        Assert.Equal(1200m, lines.Credit(accounts[CompanyAccountRole.SalesRevenue]));
        Assert.Equal(168m, lines.Credit(accounts[CompanyAccountRole.VatPayable]));
    }

    private static object SaleTemplate(string name, string paymentType, string debitRole) => new
    {
        screenCode = "SALES_INVOICE",
        definition = new
        {
            nameAr = name, nameEn = name, triggerType = "FieldCondition", triggerFieldName = "PaymentType", triggerFieldValue = paymentType,
            lines = new object[]
            {
                new { lineNumber = 1, direction = "Debit", accountSourceType = "FromCompany", accountResolverKey = debitRole,
                      amountFormulaType = "DirectField", amountFieldName = "TotalAmount", conditionType = "None", costCenters = Array.Empty<object>() },
                new { lineNumber = 2, direction = "Credit", accountSourceType = "FromCompany", accountResolverKey = "SalesRevenue",
                      amountFormulaType = "DirectField", amountFieldName = "NetAmount", conditionType = "None", costCenters = Array.Empty<object>() },
                // Tax as 14% of net — the design notes' PercentageOf, rather than the invoice's own tax field.
                new { lineNumber = 3, direction = "Credit", accountSourceType = "FromCompany", accountResolverKey = "VatPayable",
                      amountFormulaType = "PercentageOf", amountFieldName = "NetAmount", amountPercentage = 14m, conditionType = "None",
                      costCenters = Array.Empty<object>() }
            }
        }
    };

    [Fact]
    public async Task Templates_triggered_on_payment_type_each_take_only_their_own_invoices()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        (await client.PostAsJsonAsync(TemplatesUrl, SaleTemplate("بيع نقدي", "Cash", "Cash"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(TemplatesUrl, SaleTemplate("بيع آجل", "Credit", "DefaultReceivable"))).EnsureSuccessStatusCode();

        var customerId = await SeedCustomerAsync(ids);
        var cashInvoice = await SeedSalesInvoiceAsync(ids, customerId, SalesInvoicePaymentType.Cash);
        var creditInvoice = await SeedSalesInvoiceAsync(ids, customerId, SalesInvoicePaymentType.Credit);
        (await client.PostAsync($"/api/v1/sales/invoices/{cashInvoice}/post", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/sales/invoices/{creditInvoice}/post", null)).EnsureSuccessStatusCode();

        var cashEntry = await EntryOfAsync(ids.CompanyId, db => db.SalesInvoices.Where(i => i.Id == cashInvoice).Select(i => i.JournalEntryId).SingleAsync());
        var creditEntry = await EntryOfAsync(ids.CompanyId, db => db.SalesInvoices.Where(i => i.Id == creditInvoice).Select(i => i.JournalEntryId).SingleAsync());
        var cashLines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, cashEntry!.Value);
        Assert.Equal(1368m, cashLines.Debit(accounts[CompanyAccountRole.Cash]));
        Assert.Equal(168m, cashLines.Credit(accounts[CompanyAccountRole.VatPayable]));   // 1 200 × 14%
        Assert.Equal(1368m, (await PostingSeed.LinesOfAsync(factory, ids.CompanyId, creditEntry!.Value)).Debit(accounts[CompanyAccountRole.DefaultReceivable]));

        // The screen preview shows which template takes a cash sale, and the formula it used.
        var preview = await client.PostAsJsonAsync($"{TemplatesUrl}/preview-screen", new
        {
            screenCode = "SALES_INVOICE",
            sample = new { values = new Dictionary<string, decimal> { ["NetAmount"] = 870m, ["TotalAmount"] = 991.80m }, texts = new Dictionary<string, string> { ["PaymentType"] = "Cash" } }
        });
        preview.EnsureSuccessStatusCode();
        var items = (await preview.Content.ReadFromJsonAsync<List<ScreenPreviewItem>>())!;
        Assert.True(items.Single(i => i.NameAr == "بيع نقدي").Preview.TriggerMatched);
        Assert.False(items.Single(i => i.NameAr == "بيع آجل").Preview.TriggerMatched);
        var vatLine = items.Single(i => i.NameAr == "بيع نقدي").Preview.Lines.Single(l => l.LineNumber == 3);
        Assert.Equal(121.80m, vatLine.Amount);
        Assert.Contains("870.00 × 0.14", vatLine.AmountFormula);
    }

    [Fact]
    public async Task Cancelling_a_posted_sales_invoice_reverses_its_entry()
    {
        var (ids, _, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "SALES_INVOICE");
        var invoiceId = await SeedSalesInvoiceAsync(ids, await SeedCustomerAsync(ids));
        (await client.PostAsync($"/api/v1/sales/invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        (await client.PostAsync($"/api/v1/sales/invoices/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var invoice = await db.SalesInvoices.SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(SalesInvoiceStatus.Cancelled, invoice.Status);
        var reversal = await db.JournalEntries.SingleAsync(e => e.ReversalOfEntryId == invoice.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
        Assert.Equal(1368m, reversal.TotalDebit);
    }

    [Fact]
    public async Task A_delivery_order_posts_the_cost_of_what_left_the_warehouse()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "SALES_DELIVERY_ORDER");
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, averageCost: 0.40m);
        var customerId = await SeedCustomerAsync(ids);
        var invoiceId = await SeedSalesInvoiceAsync(ids, customerId);

        long deliveryId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var delivery = new DeliveryOrder
            {
                CompanyId = ids.CompanyId, BranchId = ids.BranchId, CustomerId = customerId, WarehouseId = ids.MainWarehouseId,
                DeliveryNumber = $"DO-{Random.Shared.Next(100000, 999999)}", DeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SourceInvoiceId = invoiceId, Status = DeliveryOrderStatus.Draft
            };
            delivery.Lines.Add(new DeliveryOrderLine { LineNumber = 1, ItemId = ids.EspressoItemId, Quantity = 1000m });
            db.DeliveryOrders.Add(delivery);
            await db.SaveChangesAsync();
            deliveryId = delivery.Id;
        }

        (await client.PostAsync($"/api/v1/sales/delivery-orders/{deliveryId}/post", null)).EnsureSuccessStatusCode();

        var entryId = await EntryOfAsync(ids.CompanyId, db => db.DeliveryOrders.Where(d => d.Id == deliveryId).Select(d => d.JournalEntryId).SingleAsync());
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, entryId!.Value);
        Assert.Equal(400m, lines.Debit(accounts[CompanyAccountRole.CostOfGoodsSold]));   // 1 000 g × 0.40
        Assert.Equal(400m, lines.Credit(accounts[CompanyAccountRole.Inventory]));
    }

    [Fact]
    public async Task A_sales_return_reverses_revenue_at_the_source_invoice_tax_rate_and_restocks_at_cost()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "SALES_RETURN");
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, averageCost: 0.40m);
        var customerId = await SeedCustomerAsync(ids);
        var invoiceId = await SeedSalesInvoiceAsync(ids, customerId);

        long returnId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var salesReturn = new SalesReturn
            {
                CompanyId = ids.CompanyId, BranchId = ids.BranchId, CustomerId = customerId, WarehouseId = ids.MainWarehouseId,
                SourceInvoiceId = invoiceId, ReturnNumber = $"SR-{Random.Shared.Next(100000, 999999)}",
                ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow), Reason = "تالف", Status = SalesReturnStatus.Draft
            };
            salesReturn.Lines.Add(new SalesReturnLine { LineNumber = 1, ItemId = ids.EspressoItemId, Quantity = 250m, UnitPrice = 1.20m });
            db.SalesReturns.Add(salesReturn);
            await db.SaveChangesAsync();
            returnId = salesReturn.Id;
        }

        (await client.PostAsync($"/api/v1/sales/returns/{returnId}/post", null)).EnsureSuccessStatusCode();

        var entryId = await EntryOfAsync(ids.CompanyId, db => db.SalesReturns.Where(r => r.Id == returnId).Select(r => r.JournalEntryId).SingleAsync());
        var lines = await PostingSeed.LinesOfGroupAsync(factory, ids.CompanyId, entryId!.Value);
        Assert.Equal(300m, lines.Debit(accounts[CompanyAccountRole.SalesReturn]));     // 250 × 1.20
        Assert.Equal(42m, lines.Debit(accounts[CompanyAccountRole.VatPayable]));       // 14% like the invoice
        Assert.Equal(342m, lines.Credit(accounts[CompanyAccountRole.DefaultReceivable]));
        Assert.Equal(100m, lines.Debit(accounts[CompanyAccountRole.Inventory]));       // 250 × 0.40
        Assert.Equal(100m, lines.Credit(accounts[CompanyAccountRole.CostOfGoodsSold]));
    }

    // ------------------------------------------------------------------ purchasing

    [Fact]
    public async Task A_purchase_return_above_average_cost_books_the_difference_as_price_variance()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "PURCHASING_PURCHASE_RETURN");
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, averageCost: 0.40m);

        long returnId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var purchaseReturn = new PurchaseReturn
            {
                CompanyId = ids.CompanyId, BranchId = ids.BranchId, SupplierId = ids.SupplierId, WarehouseId = ids.MainWarehouseId,
                ReturnNumber = $"PR-{Random.Shared.Next(100000, 999999)}", ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Reason = PurchaseReturnReason.Damaged, Status = PurchaseReturnStatus.Draft
            };
            purchaseReturn.Lines.Add(new PurchaseReturnLine { LineNumber = 1, ItemId = ids.EspressoItemId, Quantity = 1000m, UnitCost = 0.50m, UnitId = ids.GramUnitId, BaseQuantity = 1000m, BaseUnitCost = 0.50m });
            db.PurchaseReturns.Add(purchaseReturn);
            await db.SaveChangesAsync();
            returnId = purchaseReturn.Id;
        }

        (await client.PostAsync($"/api/v1/purchasing/purchase-returns/{returnId}/post", null)).EnsureSuccessStatusCode();

        var entryId = await EntryOfAsync(ids.CompanyId, db => db.PurchaseReturns.Where(r => r.Id == returnId).Select(r => r.JournalEntryId).SingleAsync());
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, entryId!.Value);
        Assert.Equal(500m, lines.Debit(accounts[CompanyAccountRole.DefaultPayable]));         // 1 000 × 0.50, no invoice → no tax
        Assert.Equal(400m, lines.Credit(accounts[CompanyAccountRole.Inventory]));             // 1 000 × 0.40 average
        Assert.Equal(100m, lines.Credit(accounts[CompanyAccountRole.PurchasePriceVariance]));
    }

    // ------------------------------------------------------------------ inventory

    [Fact]
    public async Task A_stock_out_and_a_waste_record_both_post_at_the_average_cost()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(client, "INVENTORY_STOCK_OUT");
        await PostingSeed.ActivateDefaultAsync(client, "INVENTORY_WASTE");
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, averageCost: 0.40m);

        var stockOut = await client.PostAsJsonAsync("/api/v1/inventory/stock-out", new
        {
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            sourceWarehouseId = ids.MainWarehouseId,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 100m, unitCost = 0m } }
        });
        stockOut.EnsureSuccessStatusCode();
        var stockOutId = (await stockOut.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
        (await client.PostAsync($"/api/v1/inventory/stock-out/{stockOutId}/post", null)).EnsureSuccessStatusCode();

        var waste = await client.PostAsJsonAsync("/api/v1/inventory/waste-records", new
        {
            warehouseId = ids.MainWarehouseId, itemId = ids.EspressoItemId, quantity = 50m,
            wasteDate = DateOnly.FromDateTime(DateTime.UtcNow), reason = "اتبلّ"
        });
        waste.EnsureSuccessStatusCode();
        var wasteId = (await waste.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var stockOutEntry = await EntryOfAsync(ids.CompanyId, db => db.WarehouseDocuments.Where(d => d.Id == stockOutId).Select(d => d.JournalEntryId).SingleAsync());
        var wasteEntry = await EntryOfAsync(ids.CompanyId, db => db.WasteRecords.Where(w => w.Id == wasteId).Select(w => w.JournalEntryId).SingleAsync());

        Assert.Equal(40m, (await PostingSeed.LinesOfAsync(factory, ids.CompanyId, stockOutEntry!.Value)).Credit(accounts[CompanyAccountRole.Inventory]));
        Assert.Equal(20m, (await PostingSeed.LinesOfAsync(factory, ids.CompanyId, wasteEntry!.Value)).Debit(accounts[CompanyAccountRole.InventoryAdjustment]));
    }
}
