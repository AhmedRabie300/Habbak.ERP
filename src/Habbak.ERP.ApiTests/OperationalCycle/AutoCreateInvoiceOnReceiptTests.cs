using System.Net.Http.Json;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Gap G-6: `AutoCreateInvoiceOnReceipt` was stored on PurchaseCycleSettings and editable from the
/// settings screen, but no handler read it — flipping it did nothing at all. These tests pin the
/// behaviour it now drives, and the off-case that guarantees turning it on stays a real choice.
/// </summary>
public class AutoCreateInvoiceOnReceiptTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<(CycleIds Ids, long CompanyId, HttpClient Client, long OrderId)> ArrangeAsync(bool autoCreate)
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetPurchaseCycleAsync(factory, companyId, autoCreate);
        var orderId = await CycleSeed.SeedConfirmedPurchaseOrderAsync(factory, ids, espressoQty: 5000m, espressoPrice: 0.50m);
        return (ids, companyId, CreateClient(companyId), orderId);
    }

    private static async Task<long> CreateReceiptAsync(
        HttpClient client, CycleIds ids, long orderId, decimal accepted, decimal rejected = 0m, long? rejectedWarehouseId = null)
    {
        var create = await client.PostAsJsonAsync("/api/v1/purchasing/goods-receipts", new
        {
            warehouseId = ids.MainWarehouseId,
            receiptDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            purchaseOrderId = orderId,
            lines = new[]
            {
                new
                {
                    itemId = ids.EspressoItemId,
                    quantity = accepted + rejected,
                    acceptedQuantity = accepted,
                    rejectedQuantity = rejected,
                    rejectedWarehouseId,
                    // Both are required by the validator whenever anything is rejected.
                    rejectedReason = rejected > 0 ? "تلف في التغليف" : null,
                    unitCost = 0.50m,
                    unitId = ids.GramUnitId,
                    qualityCheckStatus = "Passed"
                }
            }
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    [Fact]
    public async Task Posting_a_receipt_drafts_the_matching_supplier_invoice()
    {
        var (ids, companyId, client, orderId) = await ArrangeAsync(autoCreate: true);
        var receiptId = await CreateReceiptAsync(client, ids, orderId, accepted: 5000m);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var invoice = await db.PurchaseInvoices.Include(i => i.Lines).SingleAsync();

        Assert.Equal(receiptId, invoice.GoodsReceiptId);
        Assert.Equal(orderId, invoice.PurchaseOrderId);
        Assert.Equal(ids.SupplierId, invoice.SupplierId);

        // Drafted, never posted: what the supplier actually bills still needs a human to reconcile.
        Assert.Equal(PurchaseInvoiceStatus.Draft, invoice.Status);

        Assert.Equal(2500m, invoice.TotalAmount);   // 5000 x 0.50
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(ids.EspressoItemId, line.ItemId);
        Assert.Equal(5000m, line.Quantity);
        Assert.Equal(0.50m, line.UnitPrice);
    }

    [Fact]
    public async Task Nothing_is_drafted_while_the_flag_is_off()
    {
        var (ids, companyId, client, orderId) = await ArrangeAsync(autoCreate: false);
        var receiptId = await CreateReceiptAsync(client, ids, orderId, accepted: 5000m);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Equal(0, await db.PurchaseInvoices.CountAsync());

        // The receipt itself still posts and still moves stock - the flag only adds the invoice.
        Assert.Equal(5000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task Rejected_quantities_are_left_off_the_payable()
    {
        var (ids, companyId, client, orderId) = await ArrangeAsync(autoCreate: true);

        // 4000 accepted into the main warehouse, 1000 rejected into the branch one (quarantine).
        var receiptId = await CreateReceiptAsync(
            client, ids, orderId, accepted: 4000m, rejected: 1000m, rejectedWarehouseId: ids.BranchWarehouseId);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var invoice = await db.PurchaseInvoices.Include(i => i.Lines).SingleAsync();

        // Billed on what was kept, not on what arrived.
        Assert.Equal(2000m, invoice.TotalAmount);   // 4000 x 0.50
        Assert.Equal(4000m, Assert.Single(invoice.Lines).Quantity);

        // Both quantities still entered stock, each in its own warehouse.
        Assert.Equal(4000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
        Assert.Equal(1000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task A_fully_rejected_receipt_produces_no_invoice_at_all()
    {
        var (ids, companyId, client, orderId) = await ArrangeAsync(autoCreate: true);
        var receiptId = await CreateReceiptAsync(
            client, ids, orderId, accepted: 0m, rejected: 5000m, rejectedWarehouseId: ids.BranchWarehouseId);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);

        // An empty payable would be worse than none.
        Assert.Equal(0, await db.PurchaseInvoices.CountAsync());
    }

    [Fact]
    public async Task Due_date_follows_the_suppliers_payment_terms()
    {
        var (ids, companyId, client, orderId) = await ArrangeAsync(autoCreate: true);
        var receiptId = await CreateReceiptAsync(client, ids, orderId, accepted: 5000m);

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var invoice = await db.PurchaseInvoices.SingleAsync();

        // The seeded supplier is Net30.
        Assert.Equal(invoice.InvoiceDate.AddDays(30), invoice.DueDate);
    }

    private sealed record CreatedResponse(long Id);
}
