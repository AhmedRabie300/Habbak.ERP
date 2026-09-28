using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.ApiTests.OperationalCycle;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Units on purchasing lines (Docs/Modules/Fixes-Batch-2026-09-19.md): every line keeps its unit's
/// factor and its base quantity and cost; stock moves in base units. Before, "2 bags" received added
/// 2 grams.
/// </summary>
public class PurchasingUnitsTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Client(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private static async Task<long> IdOf(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    /// <summary>A company with espresso bought in 1000 g bags, and a purchase cycle open enough to test each document alone.</summary>
    private async Task<(CycleIds Ids, long Bag, HttpClient Client)> ArrangeAsync(bool autoReceiptOnInvoice = false)
    {
        var ids = await CycleSeed.SeedAsync(factory, Random.Shared.NextInt64(1, long.MaxValue));
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var bag = new UnitOfMeasure
            {
                CompanyId = ids.CompanyId, Code = $"PBAG-{ids.CompanyId % 100000000}", NameAr = "شكارة", NameEn = "Bag", Category = UnitCategory.Weight, IsActive = true
            };
            db.UnitsOfMeasure.Add(bag);
            await db.SaveChangesAsync();
            db.ItemUnitConversions.Add(new ItemUnitConversion { ItemId = ids.EspressoItemId, AlternateUnitOfMeasureId = bag.Id, ConversionFactor = 1000m });
            await db.SaveChangesAsync();

            var client = Client(ids.CompanyId);
            (await client.PutAsJsonAsync("/api/v1/purchasing/settings/purchase-cycle", new
            {
                cycleType = "Direct", requiresPurchaseRequest = false, requiresQuotation = false, requiresPurchaseOrder = false,
                requiresGoodsReceipt = true, allowInvoiceWithoutOrder = true, allowReceiptWithoutInvoice = true,
                autoCreateReceiptOnInvoicePost = autoReceiptOnInvoice, autoCreateInvoiceOnReceipt = false,
                requiresApprovalForPurchaseOrder = false, requiresApprovalForInvoice = false, defaultPaymentTerms = "Net30", capitalizeAdditionalCosts = true
            })).EnsureSuccessStatusCode();

            await using var s = factory.CreateDirectDbContext(ids.CompanyId);
            (await s.Suppliers.SingleAsync(x => x.Id == ids.SupplierId)).DefaultWarehouseId = ids.MainWarehouseId;
            await s.SaveChangesAsync();
            return (ids, bag.Id, client);
        }
    }

    private static object OrderBody(CycleIds ids, params object[] lines) => new
    {
        branchId = (long?)null, orderDate = Today, supplierId = ids.SupplierId, purchaseRequestId = (long?)null, currencyCode = "EGP",
        exchangeRate = 1m, paymentTerms = "Net30", deliveryTerms = (string?)null, expectedDeliveryDate = (DateOnly?)null,
        deliveryAddress = (string?)null, taxAmount = 0m, discountAmount = (decimal?)null, discountReason = (string?)null, notes = (string?)null, lines
    };

    private static object OrderLine(long itemId, decimal quantity, decimal unitPrice, long? unitId) =>
        new { itemId, quantity, unitPrice, discountAmount = (decimal?)null, unitId, expectedDeliveryDate = (DateOnly?)null, weight = (decimal?)null };

    private static object InvoiceBody(CycleIds ids, decimal additionalCosts, string? allocation, params object[] lines) => new
    {
        branchId = ids.BranchId, invoiceDate = Today, dueDate = Today.AddDays(30), supplierId = ids.SupplierId, supplierInvoiceNumber = "S-1",
        purchaseOrderId = (long?)null, goodsReceiptId = (long?)null, warehouseId = (long?)ids.MainWarehouseId, currencyCode = "EGP",
        exchangeRate = 1m, paymentTerms = "Net30", taxAmount = 0m, additionalCosts, additionalCostAllocationMethod = allocation, lines
    };

    private static object InvoiceLine(long itemId, decimal quantity, decimal unitPrice, long? unitId) =>
        new { itemId, quantity, receivedQuantity = 0m, unitPrice, discountAmount = (decimal?)null, unitId, allocationPercentage = (decimal?)null, weight = (decimal?)null };

    private static object ReceiptBody(CycleIds ids, long orderId, decimal quantity, decimal unitCost, long? unitId, string? varianceReason = null) => new
    {
        warehouseId = ids.MainWarehouseId, receiptDate = Today, purchaseOrderId = (long?)orderId, purchaseInvoiceId = (long?)null,
        lines = new[]
        {
            new
            {
                itemId = ids.EspressoItemId, quantity, acceptedQuantity = quantity, rejectedQuantity = 0m, unitCost, unitId,
                varianceReason, qualityCheckStatus = "Passed"
            }
        }
    };

    private async Task<long> ConfirmedOrderInBagsAsync(CycleIds ids, long bag, HttpClient client, decimal bags = 2)
    {
        var orderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, OrderLine(ids.EspressoItemId, bags, 500m, bag))));
        (await client.PostAsync($"/api/v1/purchasing/purchase-orders/{orderId}/send", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/purchasing/purchase-orders/{orderId}/confirm", null)).EnsureSuccessStatusCode();
        return orderId;
    }

    // ----------------------------------------------------------------------------- receipt

    [Fact]
    public async Task Receiving_two_bags_adds_2000_grams_at_the_cost_of_a_gram()
    {
        var (ids, bag, client) = await ArrangeAsync();
        var orderId = await ConfirmedOrderInBagsAsync(ids, bag, client);

        var receiptId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", ReceiptBody(ids, orderId, 2, 500m, bag)));
        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId));
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var movement = await db.StockTransactions.SingleAsync(t => t.SourceDocumentType == "GoodsReceipt" && t.SourceDocumentId == receiptId);
        Assert.Equal(2000m, movement.Quantity);
        Assert.Equal(0.5m, movement.UnitCost);

        var line = await db.GoodsReceiptLines.SingleAsync(l => l.GoodsReceiptId == receiptId);
        Assert.Equal(1000m, line.UnitFactor);
        Assert.Equal(2000m, line.BaseQuantity);
        Assert.Equal(0.5m, line.BaseUnitCost);

        var order = await db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);
        Assert.Equal(2m, order.Lines.Single().ReceivedQuantity);
        Assert.Equal(PurchaseOrderStatus.FullyReceived, order.Status);
    }

    [Fact]
    public async Task A_receipt_in_grams_against_an_order_in_bags_counts_through_base_units()
    {
        var (ids, bag, client) = await ArrangeAsync();
        var orderId = await ConfirmedOrderInBagsAsync(ids, bag, client);

        // Half the order, weighed in grams (no unit = the base unit).
        var receiptId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts",
            ReceiptBody(ids, orderId, 1000, 0.5m, null, varianceReason: "نص الكمية بس وصلت")));
        var receipt = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/goods-receipts/{receiptId}");
        var line = receipt.GetProperty("lines")[0];
        Assert.Equal(2000m, line.GetProperty("expectedQuantity").GetDecimal());   // 2 bags, in grams
        Assert.Equal(ids.GramUnitId, line.GetProperty("unitId").GetInt64());

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var order = await db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);
        Assert.Equal(1m, order.Lines.Single().ReceivedQuantity);   // one bag of the two
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(1000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    // ----------------------------------------------------------------------------- order, request

    [Fact]
    public async Task An_order_and_a_request_in_bags_keep_their_base_quantity()
    {
        var (ids, bag, client) = await ArrangeAsync();

        var orderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, OrderLine(ids.EspressoItemId, 2, 500m, bag))));
        var orderLine = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}")).GetProperty("lines")[0];
        Assert.Equal(1000m, orderLine.GetProperty("unitFactor").GetDecimal());
        Assert.Equal(2000m, orderLine.GetProperty("baseQuantity").GetDecimal());
        Assert.Equal(0.5m, orderLine.GetProperty("baseUnitCost").GetDecimal());
        Assert.Equal(1000m, orderLine.GetProperty("totalPrice").GetDecimal());

        var requestId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, priority = "Normal", reason = (string?)null, notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2m, unitId = (long?)bag, notes = (string?)null } }
        }));
        var requestLine = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-requests/{requestId}")).GetProperty("lines")[0];
        Assert.Equal(2000m, requestLine.GetProperty("baseQuantity").GetDecimal());
        Assert.Equal(bag, requestLine.GetProperty("unitId").GetInt64());
    }

    // ----------------------------------------------------------------------------- invoice

    [Fact]
    public async Task An_invoice_in_bags_records_its_cost_per_gram_and_its_auto_receipt_adds_grams()
    {
        var (ids, bag, client) = await ArrangeAsync(autoReceiptOnInvoice: true);

        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, 0m, null, InvoiceLine(ids.EspressoItemId, 2, 500m, bag))));
        var line = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-invoices/{invoiceId}")).GetProperty("lines")[0];
        Assert.Equal(1000m, line.GetProperty("totalPrice").GetDecimal());
        Assert.Equal(2000m, line.GetProperty("baseQuantity").GetDecimal());
        Assert.Equal(0.5m, line.GetProperty("baseUnitCost").GetDecimal());

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var receipt = await db.GoodsReceipts.Include(r => r.Lines).SingleAsync(r => r.PurchaseInvoiceId == invoiceId);
        var receiptLine = receipt.Lines.Single();
        Assert.Equal(bag, receiptLine.UnitId);
        Assert.Equal(2m, receiptLine.Quantity);
        Assert.Equal(2000m, receiptLine.BaseQuantity);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receipt.Id}/post", null)).EnsureSuccessStatusCode();
        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task Additional_costs_by_quantity_are_spread_on_base_quantities()
    {
        var (ids, bag, client) = await ArrangeAsync();

        // 2 bags of espresso (2000 g) and 1000 ml of milk: 2000 : 1000.
        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, 300m, "ByQuantity", InvoiceLine(ids.EspressoItemId, 2, 500m, bag), InvoiceLine(ids.MilkItemId, 1000, 0.02m, null))));

        var lines = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-invoices/{invoiceId}")).GetProperty("lines");
        var byItem = lines.EnumerateArray().ToDictionary(l => l.GetProperty("itemId").GetInt64(), l => l.GetProperty("allocatedAdditionalCost").GetDecimal());
        Assert.Equal(200m, byItem[ids.EspressoItemId]);
        Assert.Equal(100m, byItem[ids.MilkItemId]);
    }

    // ----------------------------------------------------------------------------- return

    [Fact]
    public async Task Returning_two_bags_takes_2000_grams_out()
    {
        var (ids, bag, client) = await ArrangeAsync();
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, 0.5m);

        var returnId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", new
        {
            branchId = (long?)null, returnDate = Today, supplierId = ids.SupplierId, purchaseInvoiceId = (long?)null, warehouseId = ids.MainWarehouseId,
            reason = "Damaged", notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2m, unitCost = 500m, unitId = (long?)bag, batchNumber = (string?)null } }
        }));
        (await client.PostAsync($"/api/v1/purchasing/purchase-returns/{returnId}/post", null)).EnsureSuccessStatusCode();

        Assert.Equal(3000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId));
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var movement = await db.StockTransactions.SingleAsync(t => t.SourceDocumentType == "PurchaseReturn" && t.SourceDocumentId == returnId);
        Assert.Equal(2000m, movement.Quantity);
    }

    // ----------------------------------------------------------------------------- units allowed

    [Fact]
    public async Task A_unit_the_item_does_not_have_is_refused_on_every_document()
    {
        var (ids, _, client) = await ArrangeAsync();
        var milli = ids.MilliUnitId;   // milk's unit, not espresso's

        Assert.Equal("PUR-UNIT-NOT-ALLOWED", await ErrorCodeAsync(
            await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, OrderLine(ids.EspressoItemId, 2, 1m, milli)))));
        Assert.Equal("PUR-UNIT-NOT-ALLOWED", await ErrorCodeAsync(
            await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, 0m, null, InvoiceLine(ids.EspressoItemId, 2, 1m, milli)))));
        Assert.Equal("PUR-UNIT-NOT-ALLOWED", await ErrorCodeAsync(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, priority = "Normal", reason = (string?)null, notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2m, unitId = (long?)milli, notes = (string?)null } }
        })));
        Assert.Equal("PUR-UNIT-NOT-ALLOWED", await ErrorCodeAsync(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-returns", new
        {
            branchId = (long?)null, returnDate = Today, supplierId = ids.SupplierId, purchaseInvoiceId = (long?)null, warehouseId = ids.MainWarehouseId,
            reason = "Damaged", notes = (string?)null,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2m, unitCost = 1m, unitId = (long?)milli, batchNumber = (string?)null } }
        })));
    }

    [Fact]
    public async Task A_line_without_a_unit_is_in_the_base_unit()
    {
        var (ids, _, client) = await ArrangeAsync();

        var orderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, OrderLine(ids.EspressoItemId, 700, 0.5m, null))));
        var line = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}")).GetProperty("lines")[0];

        Assert.Equal(ids.GramUnitId, line.GetProperty("unitId").GetInt64());
        Assert.Equal(1m, line.GetProperty("unitFactor").GetDecimal());
        Assert.Equal(700m, line.GetProperty("baseQuantity").GetDecimal());
    }

    [Fact]
    public async Task Editing_with_a_refused_unit_leaves_the_document_as_it_was()
    {
        var (ids, bag, client) = await ArrangeAsync();
        var orderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(ids, OrderLine(ids.EspressoItemId, 2, 500m, bag))));
        var rowVersion = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}")).GetProperty("rowVersion").GetString();

        var refused = await client.PutAsJsonAsync($"/api/v1/purchasing/purchase-orders/{orderId}", new
        {
            rowVersion, branchId = (long?)null, orderDate = Today, supplierId = ids.SupplierId, currencyCode = "EGP", exchangeRate = 1m,
            paymentTerms = "Net30", deliveryTerms = (string?)null, expectedDeliveryDate = (DateOnly?)null, deliveryAddress = (string?)null,
            taxAmount = 0m, discountAmount = (decimal?)null, discountReason = (string?)null, notes = (string?)null,
            lines = new[] { OrderLine(ids.EspressoItemId, 9, 1m, ids.MilliUnitId) }
        });

        Assert.Equal("PUR-UNIT-NOT-ALLOWED", await ErrorCodeAsync(refused));
        var lines = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}")).GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(2m, lines[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(bag, lines[0].GetProperty("unitId").GetInt64());
    }
}
