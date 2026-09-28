using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// The QA batch of 2026-09-21 (Docs/My Remarks/Remarks4.md): the purchase cycle settings that were
/// stored but never enforced (item 6), the source-document panels of the order and invoice screens
/// (items 4 and 5), spreading one supplier payment across invoices (item 7), and tying a purchase
/// return to the invoice lines it came off (item 8).
/// </summary>
public class PurchasingQaBatch20260921Tests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Client(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static async Task<long> IdOf(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    /// <summary>Fails with the server's own message, so a refused call says why.</summary>
    private static async Task OkAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private async Task<(CycleIds Ids, HttpClient Client)> ArrangeAsync()
    {
        var ids = await CycleSeed.SeedAsync(factory, Random.Shared.NextInt64(1, long.MaxValue));
        return (ids, Client(ids.CompanyId));
    }

    /// <summary>Writes the settings row directly — these tests set one flag and leave the rest alone.</summary>
    private async Task SettingsAsync(long companyId, Action<PurchaseCycleSettings> configure)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var row = await db.PurchaseCycleSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == companyId);
        if (row is null)
        {
            row = new PurchaseCycleSettings { CompanyId = companyId, RequiresGoodsReceipt = false };
            db.PurchaseCycleSettingsRows.Add(row);
        }

        configure(row);
        await db.SaveChangesAsync();
    }

    private static object OrderBody(
        CycleIds ids, long? requestId = null, long? rfqId = null, decimal quantity = 1000m, decimal price = 0.5m, long? purchaseRequestLineId = null) => new
    {
        branchId = ids.BranchId,
        orderDate = Today,
        supplierId = ids.SupplierId,
        purchaseRequestId = requestId,
        rfqId,
        currencyCode = "EGP",
        exchangeRate = 1m,
        paymentTerms = (string?)null,
        deliveryTerms = (string?)null,
        expectedDeliveryDate = (DateOnly?)null,
        deliveryAddress = (string?)null,
        taxAmount = 0m,
        discountAmount = (decimal?)null,
        discountReason = (string?)null,
        notes = (string?)null,
        lines = new[]
        {
            new
            {
                itemId = ids.EspressoItemId, quantity, unitPrice = price, discountAmount = (decimal?)null, unitId = (long?)null,
                expectedDeliveryDate = (DateOnly?)null, weight = (decimal?)null, purchaseRequestLineId
            }
        }
    };

    private static object InvoiceBody(
        CycleIds ids, long? orderId = null, decimal quantity = 1000m, decimal price = 0.5m,
        decimal additionalCosts = 0m, string? allocationMethod = null, long? warehouseId = null) => new
    {
        branchId = ids.BranchId,
        invoiceDate = Today,
        dueDate = Today.AddDays(30),
        supplierId = ids.SupplierId,
        supplierInvoiceNumber = "QA-0001",
        purchaseOrderId = orderId,
        goodsReceiptId = (long?)null,
        warehouseId,
        currencyCode = "EGP",
        exchangeRate = 1m,
        paymentTerms = (string?)null,
        taxAmount = 0m,
        discountAmount = (decimal?)null,
        discountReason = (string?)null,
        additionalCosts,
        additionalCostAllocationMethod = allocationMethod,
        commissionRate = (decimal?)null,
        commissionAmount = (decimal?)null,
        commissionAccountId = (long?)null,
        notes = (string?)null,
        lines = new[] { new { itemId = ids.EspressoItemId, quantity, receivedQuantity = 0m, unitPrice = price, discountAmount = (decimal?)null, unitId = (long?)null, allocationPercentage = (decimal?)null, weight = (decimal?)null } }
    };

    // ================================================================ item 6: the cycle settings

    [Fact]
    public async Task The_named_cycle_owns_its_document_flags()
    {
        var (ids, client) = await ArrangeAsync();

        // "Direct" means buying straight off an invoice; asking for it while requiring an order is a contradiction.
        var response = await client.PutAsJsonAsync("/api/v1/purchasing/settings/purchase-cycle", new
        {
            cycleType = "Direct", requiresPurchaseRequest = false, requiresQuotation = false, requiresPurchaseOrder = true,
            requiresGoodsReceipt = true, allowInvoiceWithoutOrder = false, allowReceiptWithoutInvoice = true,
            autoCreateReceiptOnInvoicePost = false, autoCreateInvoiceOnReceipt = false, requiresApprovalForPurchaseOrder = false,
            requiresApprovalForInvoice = false, defaultPaymentTerms = "Net30", capitalizeAdditionalCosts = true
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-CYCLE-TYPE-MISMATCH", await ErrorCodeAsync(response));
        Assert.NotNull(ids.CompanyId);
    }

    [Fact]
    public async Task The_cycle_presets_are_what_the_screen_applies()
    {
        var (_, client) = await ArrangeAsync();
        var presets = await client.GetFromJsonAsync<JsonElement>("/api/v1/purchasing/settings/purchase-cycle/presets");

        var direct = presets.EnumerateArray().Single(p => p.GetProperty("cycleType").GetString() == "Direct");
        Assert.True(direct.GetProperty("allowInvoiceWithoutOrder").GetBoolean());
        Assert.False(direct.GetProperty("requiresPurchaseOrder").GetBoolean());

        var full = presets.EnumerateArray().Single(p => p.GetProperty("cycleType").GetString() == "Full");
        Assert.True(full.GetProperty("requiresQuotation").GetBoolean());
        Assert.True(full.GetProperty("requiresPurchaseRequest").GetBoolean());
    }

    [Fact]
    public async Task Requiring_quotations_refuses_an_order_that_has_no_rfq()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s => s.RequiresQuotation = true);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-ORDER-RFQ-REQUIRED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task An_order_from_an_rfq_the_supplier_did_not_win_is_refused()
    {
        var (ids, client) = await ArrangeAsync();
        long rfqId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            // Awarded, but to nobody: no quote is selected for this supplier.
            var rfq = new RequestForQuotation
            {
                CompanyId = ids.CompanyId, BranchId = ids.BranchId, RFQNumber = $"RFQ-{Random.Shared.Next(100000, 999999)}",
                RFQDate = Today, Status = RFQStatus.Awarded
            };
            rfq.Suppliers.Add(new RFQSupplier { SupplierId = ids.SupplierId, Status = RFQSupplierStatus.Responded });
            db.RequestsForQuotation.Add(rfq);
            await db.SaveChangesAsync();
            rfqId = rfq.Id;
        }

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, rfqId: rfqId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-ORDER-RFQ-SUPPLIER-MISMATCH", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Requiring_a_goods_receipt_refuses_posting_an_ordered_invoice_nobody_received()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.RequiresGoodsReceipt = true;
            s.AutoCreateReceiptOnInvoicePost = false;
        });
        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, espressoQty: 1000m, espressoPrice: 0.5m);

        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId: orderId)));
        var response = await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-INVOICE-RECEIPT-REQUIRED", await ErrorCodeAsync(response));
    }

    /// <summary>
    /// The direct cycle receives goods against the invoice, which only exists once it is posted —
    /// so the requirement cannot gate that post, and the flag is satisfied by the receipt that follows.
    /// </summary>
    [Fact]
    public async Task Requiring_a_goods_receipt_does_not_deadlock_the_direct_cycle()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.RequiresGoodsReceipt = true;
            s.AllowInvoiceWithoutOrder = true;
            s.AutoCreateReceiptOnInvoicePost = false;
        });

        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices", InvoiceBody(ids)));
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(PurchaseInvoiceStatus.Posted, (await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceId)).Status);
    }

    [Fact]
    public async Task Auto_creating_the_receipt_satisfies_the_receipt_requirement()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.RequiresGoodsReceipt = true;
            s.AllowInvoiceWithoutOrder = true;
            s.AutoCreateReceiptOnInvoicePost = true;
        });

        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, warehouseId: ids.MainWarehouseId)));
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(1, await db.GoodsReceipts.CountAsync(r => r.PurchaseInvoiceId == invoiceId));
    }

    [Fact]
    public async Task Refusing_receipts_before_the_invoice_blocks_receiving_against_an_uninvoiced_order()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s => s.AllowReceiptWithoutInvoice = false);
        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, espressoQty: 1000m, espressoPrice: 0.5m);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", new
        {
            branchId = ids.BranchId,
            warehouseId = ids.MainWarehouseId,
            receiptDate = Today,
            purchaseOrderId = orderId,
            purchaseInvoiceId = (long?)null,
            notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 1000m, acceptedQuantity = 1000m, rejectedQuantity = 0m, rejectedReason = (string?)null, rejectedWarehouseId = (long?)null, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, expiryDate = (DateOnly?)null, varianceReason = (string?)null, qualityCheckStatus = "Passed", qualityCheckNotes = (string?)null } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-RECEIPT-INVOICE-REQUIRED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Requiring_an_order_blocks_receiving_against_an_invoice_that_has_none()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.AllowInvoiceWithoutOrder = true;
            s.RequiresPurchaseOrder = true;
        });
        var invoiceId = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, espressoQty: 1000m, espressoPrice: 0.5m);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", new
        {
            branchId = ids.BranchId,
            warehouseId = ids.MainWarehouseId,
            receiptDate = Today,
            purchaseOrderId = (long?)null,
            purchaseInvoiceId = invoiceId,
            notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 1000m, acceptedQuantity = 1000m, rejectedQuantity = 0m, rejectedReason = (string?)null, rejectedWarehouseId = (long?)null, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, expiryDate = (DateOnly?)null, varianceReason = (string?)null, qualityCheckStatus = "Passed", qualityCheckNotes = (string?)null } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-RECEIPT-ORDER-REQUIRED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task An_order_with_no_payment_terms_falls_back_to_the_supplier_then_the_settings()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s => s.DefaultPaymentTerms = SupplierPaymentTerms.Cash);
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            // The seed's supplier buys on Net30; clearing that leaves the settings to decide.
            var supplier = await db.Suppliers.SingleAsync(s => s.Id == ids.SupplierId);
            supplier.PaymentTerms = SupplierPaymentTerms.Net60;
            await db.SaveChangesAsync();
        }

        var withSupplierTerms = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids)));

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(SupplierPaymentTerms.Net60, (await check.PurchaseOrders.SingleAsync(o => o.Id == withSupplierTerms)).PaymentTerms);
    }

    [Fact]
    public async Task Capitalised_additional_costs_reach_the_stock_cost()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.AllowInvoiceWithoutOrder = true;
            s.CapitalizeAdditionalCosts = true;
            s.AutoCreateReceiptOnInvoicePost = true;
        });

        // 1 000 g at 0.50 = 500, plus 100 of freight spread by value → 0.60 per gram.
        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, additionalCosts: 100m, allocationMethod: "ByValue", warehouseId: ids.MainWarehouseId)));
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        long receiptId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            receiptId = (await db.GoodsReceipts.SingleAsync(r => r.PurchaseInvoiceId == invoiceId)).Id;
        }

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        var balance = await check.StockBalances.SingleAsync(b => b.WarehouseId == ids.MainWarehouseId && b.ItemId == ids.EspressoItemId);
        Assert.Equal(0.60m, balance.AverageCost);
    }

    [Fact]
    public async Task Additional_costs_stay_out_of_the_stock_cost_when_the_company_expenses_them()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.AllowInvoiceWithoutOrder = true;
            s.CapitalizeAdditionalCosts = false;
            s.AutoCreateReceiptOnInvoicePost = true;
        });

        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, additionalCosts: 100m, allocationMethod: "ByValue", warehouseId: ids.MainWarehouseId)));
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        long receiptId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            receiptId = (await db.GoodsReceipts.SingleAsync(r => r.PurchaseInvoiceId == invoiceId)).Id;
        }

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        var balance = await check.StockBalances.SingleAsync(b => b.WarehouseId == ids.MainWarehouseId && b.ItemId == ids.EspressoItemId);
        Assert.Equal(0.50m, balance.AverageCost);
    }

    // ================================================================ items 4 and 5: the source documents

    [Fact]
    public async Task The_order_screen_shows_what_its_request_asked_for_and_what_is_left()
    {
        var (ids, client) = await ArrangeAsync();
        var requestId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, priority = "Normal", reason = "مخزون البن خلص", notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 5000m, unitId = (long?)null, notes = (string?)null } }
        }));
        (await client.PostAsync($"/api/v1/purchasing/purchase-requests/{requestId}/submit", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/purchasing/purchase-requests/{requestId}/approve", null)).EnsureSuccessStatusCode();

        // Remarks7: an order line only counts against a request line once it names one explicitly —
        // the same "pick it from the loader" contract Remarks6 already requires of invoice lines.
        var requestLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-requests/{requestId}/order-lines"))
            .EnumerateArray().Single().GetProperty("purchaseRequestLineId").GetInt64();

        var orderId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-orders", OrderBody(ids, requestId: requestId, quantity: 2000m, purchaseRequestLineId: requestLineId)));

        var source = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/source-request");
        Assert.Equal("مخزون البن خلص", source.GetProperty("reason").GetString());
        var line = source.GetProperty("lines")[0];
        Assert.Equal(5000m, line.GetProperty("requestedBaseQuantity").GetDecimal());
        Assert.Equal(2000m, line.GetProperty("orderedOnThisOrderBaseQuantity").GetDecimal());
        Assert.Equal(3000m, line.GetProperty("remainingBaseQuantity").GetDecimal());
    }

    [Fact]
    public async Task An_order_raised_without_a_request_has_no_source_panel()
    {
        var (ids, client) = await ArrangeAsync();
        var orderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids)));

        var response = await client.GetAsync($"/api/v1/purchasing/purchase-orders/{orderId}/source-request");
        response.EnsureSuccessStatusCode();
        Assert.True(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task The_invoice_screen_shows_the_order_it_bills_and_what_actually_arrived()
    {
        var (ids, client) = await ArrangeAsync();
        await SettingsAsync(ids.CompanyId, s => s.RequiresGoodsReceipt = false);
        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, espressoQty: 1000m, espressoPrice: 0.5m);

        // 600 g of the 1 000 ordered turn up and are accepted.
        var receiptId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", new
        {
            branchId = ids.BranchId,
            warehouseId = ids.MainWarehouseId,
            receiptDate = Today,
            purchaseOrderId = orderId,
            purchaseInvoiceId = (long?)null,
            notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 600m, acceptedQuantity = 600m, rejectedQuantity = 0m, rejectedReason = (string?)null, rejectedWarehouseId = (long?)null, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, expiryDate = (DateOnly?)null, varianceReason = "توريد جزئي", qualityCheckStatus = "Passed", qualityCheckNotes = (string?)null } }
        }));
        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId: orderId, quantity: 1000m)));

        var source = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-invoices/{invoiceId}/source-order");
        var line = source.GetProperty("lines")[0];
        Assert.Equal(1000m, line.GetProperty("orderedBaseQuantity").GetDecimal());
        Assert.Equal(600m, line.GetProperty("receivedBaseQuantity").GetDecimal());
        Assert.Equal(1000m, line.GetProperty("invoicedOnThisInvoiceBaseQuantity").GetDecimal());
        Assert.Equal(0m, line.GetProperty("remainingBaseQuantity").GetDecimal());
    }

    // ================================================================ item 7: payment allocation

    private async Task<(long PaymentId, long InvoiceA, long InvoiceB, long TreasuryId)> ArrangePaymentAsync(
        CycleIds ids, HttpClient client, decimal amount)
    {
        var invoiceA = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, espressoQty: 1000m, espressoPrice: 0.5m);
        var invoiceB = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, espressoQty: 600m, espressoPrice: 0.5m);

        long treasuryId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var treasury = new Account
            {
                CompanyId = ids.CompanyId, Code = $"T-{Guid.NewGuid():N}"[..18], NameAr = "خزينة", NameEn = "Cash",
                AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
            };
            var payable = new Account
            {
                CompanyId = ids.CompanyId, Code = $"P-{Guid.NewGuid():N}"[..18], NameAr = "موردون", NameEn = "Payables",
                AccountType = AccountType.Liability, Nature = AccountNature.Credit, IsPostable = true, IsActive = true
            };
            db.Accounts.AddRange(treasury, payable);
            await db.SaveChangesAsync();

            // The payment posts Dr supplier / Cr treasury, so the supplier needs its payable account.
            (await db.Suppliers.SingleAsync(x => x.Id == ids.SupplierId)).PayableAccountId = payable.Id;
            await db.SaveChangesAsync();
            treasuryId = treasury.Id;
        }

        var paymentId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/supplier-payments", new
        {
            branchId = ids.BranchId,
            voucherDate = Today,
            treasuryAccountId = treasuryId,
            description = "سداد دفعة",
            supplierId = ids.SupplierId,
            purchaseInvoiceId = (long?)null,
            amount,
            currencyCode = "EGP",
            exchangeRate = 1m,
            baseCurrencyAmount = amount
        }));

        return (paymentId, invoiceA, invoiceB, treasuryId);
    }

    [Fact]
    public async Task A_payment_is_proposed_across_the_oldest_invoices_first()
    {
        var (ids, client) = await ArrangeAsync();
        var (_, invoiceA, invoiceB, _) = await ArrangePaymentAsync(ids, client, 600m);

        var proposal = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/purchasing/supplier-payments/allocation-proposal?supplierId={ids.SupplierId}&amount=600&currencyCode=EGP");

        var rows = proposal.EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(invoiceA, rows[0].GetProperty("purchaseInvoiceId").GetInt64());
        Assert.Equal(500m, rows[0].GetProperty("amount").GetDecimal());
        Assert.Equal(invoiceB, rows[1].GetProperty("purchaseInvoiceId").GetInt64());
        Assert.Equal(100m, rows[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task An_allocation_that_does_not_add_up_to_the_payment_is_refused()
    {
        var (ids, client) = await ArrangeAsync();
        var (paymentId, invoiceA, _, _) = await ArrangePaymentAsync(ids, client, 600m);

        var response = await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[] { new { purchaseInvoiceId = invoiceA, amount = 500m } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-PAYMENT-ALLOCATION-MISMATCH", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Allocating_more_than_an_invoice_still_owes_is_refused()
    {
        var (ids, client) = await ArrangeAsync();
        var (paymentId, invoiceA, _, _) = await ArrangePaymentAsync(ids, client, 900m);

        var response = await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[] { new { purchaseInvoiceId = invoiceA, amount = 900m } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-PAYMENT-EXCEEDS-OUTSTANDING", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Posting_an_allocated_payment_settles_every_invoice_it_names()
    {
        var (ids, client) = await ArrangeAsync();
        var (paymentId, invoiceA, invoiceB, _) = await ArrangePaymentAsync(ids, client, 600m);

        await OkAsync(await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[]
            {
                new { purchaseInvoiceId = invoiceA, amount = 500m },
                new { purchaseInvoiceId = invoiceB, amount = 100m }
            }
        }));

        await OkAsync(await client.PostAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/post", null));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var paid = await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceA);
        var partial = await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceB);
        Assert.Equal(500m, paid.AmountPaid);
        Assert.Equal(PurchaseInvoiceStatus.Paid, paid.Status);
        Assert.Equal(100m, partial.AmountPaid);
        Assert.Equal(PurchaseInvoiceStatus.PartiallyPaid, partial.Status);
    }

    [Fact]
    public async Task Reversing_an_allocated_payment_gives_every_invoice_its_balance_back()
    {
        var (ids, client) = await ArrangeAsync();
        var (paymentId, invoiceA, invoiceB, _) = await ArrangePaymentAsync(ids, client, 600m);

        await OkAsync(await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[]
            {
                new { purchaseInvoiceId = invoiceA, amount = 500m },
                new { purchaseInvoiceId = invoiceB, amount = 100m }
            }
        }));
        await OkAsync(await client.PostAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/post", null));
        await OkAsync(await client.PostAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/reverse", null));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(0m, (await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceA)).AmountPaid);
        Assert.Equal(0m, (await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceB)).AmountPaid);
        Assert.Equal(PurchaseInvoiceStatus.Posted, (await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceB)).Status);
    }

    [Fact]
    public async Task A_posted_payment_cannot_be_reallocated()
    {
        var (ids, client) = await ArrangeAsync();
        var (paymentId, invoiceA, _, _) = await ArrangePaymentAsync(ids, client, 500m);

        await OkAsync(await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[] { new { purchaseInvoiceId = invoiceA, amount = 500m } }
        }));
        await OkAsync(await client.PostAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/post", null));

        var response = await client.PutAsJsonAsync($"/api/v1/purchasing/supplier-payments/{paymentId}/allocations", new
        {
            allocations = new[] { new { purchaseInvoiceId = invoiceA, amount = 500m } }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-PAYMENT-NOT-DRAFT", await ErrorCodeAsync(response));
    }

    // ================================================================ item 8: returns against the invoice

    private async Task<long> PostedInvoiceForReturnAsync(CycleIds ids, HttpClient client)
    {
        await SettingsAsync(ids.CompanyId, s =>
        {
            s.AllowInvoiceWithoutOrder = true;
            s.RequiresGoodsReceipt = false;
        });

        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, quantity: 1000m, warehouseId: ids.MainWarehouseId)));
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();
        return invoiceId;
    }

    private static object ReturnBody(CycleIds ids, long invoiceId, long? invoiceLineId, decimal quantity) => new
    {
        branchId = ids.BranchId,
        returnDate = Today,
        supplierId = ids.SupplierId,
        purchaseInvoiceId = invoiceId,
        warehouseId = ids.MainWarehouseId,
        reason = "Damaged",
        notes = (string?)null,
        lines = new[]
        {
            new { itemId = ids.EspressoItemId, quantity, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, purchaseInvoiceLineId = invoiceLineId }
        }
    };

    [Fact]
    public async Task The_return_screen_lists_the_invoice_lines_with_what_is_still_returnable()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);

        var lines = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}");
        var line = lines.EnumerateArray().Single();
        Assert.Equal(1000m, line.GetProperty("invoicedQuantity").GetDecimal());
        Assert.Equal(0m, line.GetProperty("returnedQuantity").GetDecimal());
        Assert.Equal(1000m, line.GetProperty("returnableQuantity").GetDecimal());
    }

    [Fact]
    public async Task A_return_line_carries_the_invoice_line_it_came_off()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);
        var invoiceLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}"))
            .EnumerateArray().Single().GetProperty("purchaseInvoiceLineId").GetInt64();

        var returnId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, invoiceLineId, 200m)));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var line = await db.PurchaseReturnLines.SingleAsync(l => l.PurchaseReturnId == returnId);
        Assert.Equal(invoiceLineId, line.PurchaseInvoiceLineId);
        Assert.Equal(200m, line.Quantity);
    }

    [Fact]
    public async Task Returning_more_than_the_invoice_billed_is_refused()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);
        var invoiceLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}"))
            .EnumerateArray().Single().GetProperty("purchaseInvoiceLineId").GetInt64();

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, invoiceLineId, 1200m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-RETURN-EXCEEDS-INVOICE", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_second_return_may_only_take_what_the_first_one_left()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);
        var invoiceLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}"))
            .EnumerateArray().Single().GetProperty("purchaseInvoiceLineId").GetInt64();

        await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, invoiceLineId, 800m)));

        var remaining = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}"))
            .EnumerateArray().Single();
        Assert.Equal(800m, remaining.GetProperty("returnedQuantity").GetDecimal());
        Assert.Equal(200m, remaining.GetProperty("returnableQuantity").GetDecimal());

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, invoiceLineId, 300m));
        Assert.Equal("PUR-RETURN-EXCEEDS-INVOICE", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_return_against_an_invoice_must_name_the_invoice_line()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, null, 100m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-RETURN-INVALID-ITEM", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_return_line_from_another_invoice_is_refused()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);
        var otherInvoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, quantity: 300m, warehouseId: ids.MainWarehouseId)));
        long foreignLineId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            foreignLineId = (await db.PurchaseInvoiceLines.FirstAsync(l => l.PurchaseInvoiceId == otherInvoiceId)).Id;
        }

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, foreignLineId, 100m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-RETURN-INVALID-ITEM", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_return_with_no_invoice_still_takes_free_form_lines()
    {
        var (ids, client) = await ArrangeAsync();

        var returnId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", new
        {
            branchId = ids.BranchId,
            returnDate = Today,
            supplierId = ids.SupplierId,
            purchaseInvoiceId = (long?)null,
            warehouseId = ids.MainWarehouseId,
            reason = "Damaged",
            notes = (string?)null,
            lines = new[]
            {
                new { itemId = ids.EspressoItemId, quantity = 50m, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, purchaseInvoiceLineId = (long?)null }
            }
        }));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var line = await db.PurchaseReturnLines.SingleAsync(l => l.PurchaseReturnId == returnId);
        Assert.Null(line.PurchaseInvoiceLineId);
        Assert.Equal(50m, line.Quantity);
    }

    [Fact]
    public async Task Editing_a_return_does_not_count_its_own_lines_against_it()
    {
        var (ids, client) = await ArrangeAsync();
        var invoiceId = await PostedInvoiceForReturnAsync(ids, client);
        var invoiceLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-returns/invoice-lines?invoiceId={invoiceId}"))
            .EnumerateArray().Single().GetProperty("purchaseInvoiceLineId").GetInt64();

        var returnId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-returns", ReturnBody(ids, invoiceId, invoiceLineId, 900m)));

        string rowVersion;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            rowVersion = Convert.ToBase64String((await db.PurchaseReturns.SingleAsync(r => r.Id == returnId)).RowVersion);
        }

        // Raising the same return from 900 to 1 000 is fine — it is the only claim on that line.
        var response = await client.PutAsJsonAsync($"/api/v1/purchasing/purchase-returns/{returnId}", new
        {
            rowVersion,
            branchId = ids.BranchId,
            returnDate = Today,
            supplierId = ids.SupplierId,
            purchaseInvoiceId = invoiceId,
            warehouseId = ids.MainWarehouseId,
            reason = "Damaged",
            notes = (string?)null,
            lines = new[]
            {
                new { itemId = ids.EspressoItemId, quantity = 1000m, unitCost = 0.5m, unitId = (long?)null, batchNumber = (string?)null, purchaseInvoiceLineId = invoiceLineId }
            }
        });

        response.EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(1000m, (await check.PurchaseReturnLines.SingleAsync(l => l.PurchaseReturnId == returnId)).Quantity);
    }
}
