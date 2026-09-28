using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Billing a purchase order through its invoices (Docs/My Remarks/Remarks6.md): the supplier's open
/// orders, the lines they still owe, the cap on what one invoice may bill, and the order's
/// InvoicedQuantity and status following every save, edit and cancellation.
/// </summary>
public class PurchaseInvoiceOrderLinkingTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private async Task<(CycleIds Ids, HttpClient Client, long OrderId)> ArrangeAsync(
        decimal orderedQuantity = 1000m, decimal price = 0.5m, bool allowManualLines = true)
    {
        var ids = await CycleSeed.SeedAsync(factory, Random.Shared.NextInt64(1, long.MaxValue));
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            db.PurchaseCycleSettingsRows.Add(new PurchaseCycleSettings
            {
                CompanyId = ids.CompanyId,
                RequiresGoodsReceipt = false,
                AllowManualInvoiceLines = allowManualLines
            });
            await db.SaveChangesAsync();
        }

        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, orderedQuantity, price);
        return (ids, Client(ids.CompanyId), orderId);
    }

    private static object InvoiceBody(CycleIds ids, long? orderId, params object[] lines) => new
    {
        branchId = ids.BranchId,
        invoiceDate = Today,
        dueDate = Today.AddDays(30),
        supplierId = ids.SupplierId,
        supplierInvoiceNumber = "R6-0001",
        purchaseOrderId = orderId,
        goodsReceiptId = (long?)null,
        warehouseId = (long?)null,
        currencyCode = "EGP",
        exchangeRate = 1m,
        paymentTerms = (string?)null,
        taxAmount = 0m,
        discountAmount = (decimal?)null,
        discountReason = (string?)null,
        additionalCosts = 0m,
        additionalCostAllocationMethod = (string?)null,
        commissionRate = (decimal?)null,
        commissionAmount = (decimal?)null,
        commissionAccountId = (long?)null,
        notes = (string?)null,
        lines
    };

    private static object Line(CycleIds ids, long? orderLineId, decimal quantity, decimal price = 0.5m, long? unitId = null) => new
    {
        itemId = ids.EspressoItemId,
        purchaseOrderLineId = orderLineId,
        quantity,
        receivedQuantity = 0m,
        unitPrice = price,
        discountAmount = (decimal?)null,
        unitId,
        allocationPercentage = (decimal?)null,
        weight = (decimal?)null
    };

    private async Task<long> OrderLineIdAsync(HttpClient client, long orderId)
    {
        var lines = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/invoice-lines");
        return lines.EnumerateArray().First().GetProperty("purchaseOrderLineId").GetInt64();
    }

    // ================================================================ the pickers

    [Fact]
    public async Task A_supplier_offers_the_orders_that_still_have_something_to_bill()
    {
        var (ids, client, orderId) = await ArrangeAsync();

        var orders = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/purchasing/purchase-orders/open-for-invoice?supplierId={ids.SupplierId}");

        var order = orders.EnumerateArray().Single();
        Assert.Equal(orderId, order.GetProperty("id").GetInt64());
        Assert.Equal(1, order.GetProperty("remainingLineCount").GetInt32());
        Assert.Equal(0m, order.GetProperty("completionPercentage").GetDecimal());
    }

    [Fact]
    public async Task The_order_lists_the_quantity_each_line_still_owes()
    {
        var (_, client, orderId) = await ArrangeAsync();

        var lines = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/invoice-lines");

        var line = lines.EnumerateArray().Single();
        Assert.Equal(1000m, line.GetProperty("orderedQuantity").GetDecimal());
        Assert.Equal(0m, line.GetProperty("invoicedQuantity").GetDecimal());
        Assert.Equal(1000m, line.GetProperty("remainingQuantity").GetDecimal());
        Assert.Equal(0.5m, line.GetProperty("unitPrice").GetDecimal());
    }

    // ================================================================ billing it

    [Fact]
    public async Task Billing_part_of_an_order_leaves_it_partially_invoiced()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);

        await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 400m))));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(400m, (await db.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyInvoiced, (await db.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Billing_all_of_an_order_marks_it_invoiced()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);

        await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 1000m))));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(PurchaseOrderStatus.Invoiced, (await db.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task A_second_invoice_may_only_bill_what_the_first_one_left()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 600m))));

        var remaining = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/invoice-lines"))
            .EnumerateArray().Single();
        Assert.Equal(400m, remaining.GetProperty("remainingQuantity").GetDecimal());

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 500m)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-INVOICE-QTY-EXCEEDS-ORDER", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Billing_more_than_the_order_asked_for_is_refused()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 1200m)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-INVOICE-QTY-EXCEEDS-ORDER", await ErrorCodeAsync(response));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(0m, (await db.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
        Assert.Equal(0, await db.PurchaseInvoices.CountAsync());
    }

    /// <summary>A bag of 1 000 g billed against an order in grams counts as the 1 000 g it is.</summary>
    [Fact]
    public async Task A_line_in_another_unit_is_counted_through_its_base_quantity()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        long bagId;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var bag = new Habbak.ERP.Domain.Inventory.UnitOfMeasure
            {
                CompanyId = ids.CompanyId, Code = $"R6BAG-{ids.CompanyId % 100000000}", NameAr = "شكارة", NameEn = "Bag",
                Category = Habbak.ERP.Domain.Inventory.UnitCategory.Weight, IsActive = true
            };
            db.UnitsOfMeasure.Add(bag);
            await db.SaveChangesAsync();
            db.ItemUnitConversions.Add(new Habbak.ERP.Domain.Inventory.ItemUnitConversion
            {
                ItemId = ids.EspressoItemId, AlternateUnitOfMeasureId = bag.Id, ConversionFactor = 1000m
            });
            await db.SaveChangesAsync();
            bagId = bag.Id;
        }

        await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 1m, 500m, bagId))));

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        // One bag = 1 000 g of the 1 000 g ordered, so the order is fully billed.
        Assert.Equal(1000m, (await check.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.Invoiced, (await check.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    // ================================================================ editing and cancelling

    [Fact]
    public async Task Editing_an_invoice_gives_its_old_quantities_back_first()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 400m))));

        string rowVersion;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            rowVersion = Convert.ToBase64String((await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceId)).RowVersion);
        }

        // 400 → 900 is fine: the invoice is the only claim on that line.
        var response = await client.PutAsJsonAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}", new
        {
            rowVersion,
            branchId = ids.BranchId,
            invoiceDate = Today,
            dueDate = Today.AddDays(30),
            supplierId = ids.SupplierId,
            supplierInvoiceNumber = "R6-0001",
            purchaseOrderId = orderId,
            goodsReceiptId = (long?)null,
            currencyCode = "EGP",
            exchangeRate = 1m,
            paymentTerms = (string?)null,
            taxAmount = 0m,
            discountAmount = (decimal?)null,
            discountReason = (string?)null,
            additionalCosts = 0m,
            additionalCostAllocationMethod = (string?)null,
            commissionRate = (decimal?)null,
            commissionAmount = (decimal?)null,
            commissionAccountId = (long?)null,
            notes = (string?)null,
            lines = new[] { Line(ids, orderLineId, 900m) }
        });

        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(900m, (await check.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.PartiallyInvoiced, (await check.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task Cancelling_an_invoice_hands_its_quantities_back_to_the_order()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 1000m))));

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(0m, (await db.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
        Assert.Equal(PurchaseOrderStatus.Confirmed, (await db.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status);
    }

    [Fact]
    public async Task A_fully_billed_order_drops_off_the_supplier_list()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 1000m))));

        var orders = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/purchasing/purchase-orders/open-for-invoice?supplierId={ids.SupplierId}");

        Assert.Empty(orders.EnumerateArray());
    }

    [Fact]
    public async Task A_cancelled_order_never_appears_on_the_list()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            (await db.PurchaseOrders.SingleAsync(o => o.Id == orderId)).Status = PurchaseOrderStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var orders = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/purchasing/purchase-orders/open-for-invoice?supplierId={ids.SupplierId}");

        Assert.Empty(orders.EnumerateArray());
    }

    [Fact]
    public async Task Editing_an_invoice_sees_its_own_quantities_as_still_available()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var orderLineId = await OrderLineIdAsync(client, orderId);
        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, orderLineId, 600m))));

        var forOthers = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/invoice-lines"))
            .EnumerateArray().Single();
        var forItself = (await client.GetFromJsonAsync<JsonElement>(
                $"/api/v1/purchasing/purchase-orders/{orderId}/invoice-lines?excludeInvoiceId={invoiceId}"))
            .EnumerateArray().Single();

        Assert.Equal(400m, forOthers.GetProperty("remainingQuantity").GetDecimal());
        Assert.Equal(1000m, forItself.GetProperty("remainingQuantity").GetDecimal());
    }

    // ================================================================ the manual-line setting

    [Fact]
    public async Task A_manual_line_is_refused_when_the_company_does_not_allow_one()
    {
        var (ids, client, orderId) = await ArrangeAsync(allowManualLines: false);
        var orderLineId = await OrderLineIdAsync(client, orderId);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, orderId, Line(ids, orderLineId, 500m), Line(ids, null, 5m)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-INVOICE-MANUAL-LINE-NOT-ALLOWED", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task A_manual_line_rides_along_when_the_company_allows_one()
    {
        var (ids, client, orderId) = await ArrangeAsync(allowManualLines: true);
        var orderLineId = await OrderLineIdAsync(client, orderId);

        var invoiceId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices",
            InvoiceBody(ids, orderId, Line(ids, orderLineId, 500m), Line(ids, null, 5m))));

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var lines = await db.PurchaseInvoiceLines.Where(l => l.PurchaseInvoiceId == invoiceId).ToListAsync();
        Assert.Equal(2, lines.Count);
        Assert.Single(lines, l => l.PurchaseOrderLineId == null);
        // Only the order-linked line bills the order.
        Assert.Equal(500m, (await db.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).InvoicedQuantity);
    }

    [Fact]
    public async Task A_line_pointing_at_another_orders_line_is_refused()
    {
        var (ids, client, orderId) = await ArrangeAsync();
        var otherOrderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, espressoQty: 300m, espressoPrice: 0.5m);
        var foreignLineId = await OrderLineIdAsync(client, otherOrderId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, orderId, Line(ids, foreignLineId, 100m)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("PUR-INVOICE-LINE-NOT-IN-ORDER", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task An_invoice_with_no_order_still_bills_whatever_it_likes()
    {
        var (ids, client, _) = await ArrangeAsync();
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            (await db.PurchaseCycleSettingsRows.SingleAsync(s => s.CompanyId == ids.CompanyId)).AllowInvoiceWithoutOrder = true;
            await db.SaveChangesAsync();
        }

        var invoiceId = await IdOf(await client.PostAsJsonAsync(
            "/api/v1/purchasing/purchase-invoices", InvoiceBody(ids, null, Line(ids, null, 40m))));

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.Equal(40m, (await check.PurchaseInvoiceLines.SingleAsync(l => l.PurchaseInvoiceId == invoiceId)).Quantity);
    }
}
