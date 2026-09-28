using System.Net.Http.Json;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 2 of Docs/End-to-End-Cycle-Test.md — receiving the goods.
///
/// This class used to pin gap G-3: GoodsReceipt.PurchaseOrderId was required and no invoice
/// reference existed, so goods bought on a direct invoice could be billed but never received, and
/// the "invoice only" cycle dead-ended. Both sources are now optional-but-exactly-one, and these
/// tests cover the invoice-sourced path the gap used to make impossible.
/// </summary>
public class GoodsReceiptFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static object ReceiptBody(CycleIds ids, long? orderId, long? invoiceId, decimal quantity) => new
    {
        warehouseId = ids.MainWarehouseId,
        receiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        purchaseOrderId = orderId,
        purchaseInvoiceId = invoiceId,
        lines = new[]
        {
            new
            {
                itemId = ids.EspressoItemId,
                quantity,
                acceptedQuantity = quantity,
                rejectedQuantity = 0m,
                unitCost = 0.50m,
                unitId = ids.GramUnitId,
                qualityCheckStatus = "Passed"
            }
        }
    };

    [Fact]
    public async Task Receipt_with_neither_an_order_nor_an_invoice_is_rejected()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/goods-receipts", ReceiptBody(ids, orderId: null, invoiceId: null, quantity: 5000m));

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Receipt_naming_both_an_order_and_an_invoice_is_rejected()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, 5000m, 0.50m);
        var invoiceId = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 5000m, 0.50m);

        // Exactly one source, not both - a receipt cannot be against two agreements at once.
        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/goods-receipts", ReceiptBody(ids, orderId, invoiceId, 5000m));

        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Goods_bought_on_a_direct_invoice_can_be_received_and_move_stock()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        var invoiceId = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 5000m, 0.50m);

        var create = await client.PostAsJsonAsync(
            "/api/v1/purchasing/goods-receipts", ReceiptBody(ids, orderId: null, invoiceId: invoiceId, quantity: 5000m));
        create.EnsureSuccessStatusCode();
        var receiptId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var receipt = await db.GoodsReceipts.FirstAsync(r => r.Id == receiptId);

        Assert.Equal(invoiceId, receipt.PurchaseInvoiceId);
        Assert.Null(receipt.PurchaseOrderId);
        Assert.Equal(GoodsReceiptStatus.Posted, receipt.Status);
        Assert.Equal(ids.SupplierId, receipt.SupplierId);   // copied off the invoice

        // The whole point of the gap: this stock movement was previously unreachable, and the
        // supplier/invoice link it carries is what the StockIn workaround could never provide.
        Assert.Equal(5000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
        var movement = await db.StockTransactions.SingleAsync();
        Assert.Equal("GoodsReceipt", movement.SourceDocumentType);
        Assert.Equal(receiptId, movement.SourceDocumentId);
    }

    [Fact]
    public async Task A_draft_invoice_cannot_be_received_against()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        var invoiceId = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 5000m, 0.50m, status: PurchaseInvoiceStatus.Draft);

        var response = await client.PostAsJsonAsync(
            "/api/v1/purchasing/goods-receipts", ReceiptBody(ids, orderId: null, invoiceId: invoiceId, quantity: 5000m));

        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains("PUR-RECEIPT-INVOICE-NOT-RECEIVABLE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Receiving_an_item_the_invoice_never_listed_is_rejected()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        var invoiceId = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 5000m, 0.50m);

        var response = await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", new
        {
            warehouseId = ids.MainWarehouseId,
            receiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            purchaseInvoiceId = invoiceId,
            lines = new[]
            {
                // Milk was never on that invoice.
                new { itemId = ids.MilkItemId, quantity = 100m, acceptedQuantity = 100m, rejectedQuantity = 0m,
                      unitCost = 0.02m, unitId = ids.MilliUnitId, qualityCheckStatus = "Passed" }
            }
        });

        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains("PUR-RECEIPT-ITEM-NOT-IN-ORDER", await response.Content.ReadAsStringAsync());
    }

    private sealed record CreatedResponse(long Id);
}
