using System.Net.Http.Json;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// The second half of gap G-6. `AutoCreateReceiptOnInvoicePost` could not be implemented at all
/// until G-3 was resolved: a receipt had to name a purchase order, and a direct invoice has none.
/// With GoodsReceipt able to point at an invoice, posting one can now raise the matching receipt.
/// </summary>
public class AutoCreateReceiptOnInvoicePostTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task SetFlagAsync(long companyId, bool autoCreateReceipt)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var row = await db.PurchaseCycleSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == companyId);
        if (row is null)
        {
            row = new PurchaseCycleSettings { CompanyId = companyId };
            db.PurchaseCycleSettingsRows.Add(row);
        }
        row.AutoCreateReceiptOnInvoicePost = autoCreateReceipt;
        row.RequiresApprovalForInvoice = false;   // post straight from Draft
        // These tests are about the auto-create flag alone; the receipt requirement (Remarks4, item 6)
        // is covered by its own test and would otherwise refuse the no-receipt case below.
        row.RequiresGoodsReceipt = false;
        await db.SaveChangesAsync();
    }

    private async Task<long> SeedDraftInvoiceAsync(CycleIds ids, long? warehouseId)
        => await CycleSeed.SeedPostedInvoiceAsync(
            factory, ids, espressoQty: 5000m, espressoPrice: 0.50m,
            status: PurchaseInvoiceStatus.Draft, warehouseId: warehouseId);

    [Fact]
    public async Task Posting_an_invoice_drafts_the_matching_goods_receipt()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await SetFlagAsync(companyId, autoCreateReceipt: true);
        var invoiceId = await SeedDraftInvoiceAsync(ids, warehouseId: ids.MainWarehouseId);
        var client = CreateClient(companyId);

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var receipt = await db.GoodsReceipts.Include(r => r.Lines).SingleAsync();

        Assert.Equal(invoiceId, receipt.PurchaseInvoiceId);
        Assert.Null(receipt.PurchaseOrderId);
        Assert.Equal(ids.MainWarehouseId, receipt.WarehouseId);
        Assert.Equal(ids.SupplierId, receipt.SupplierId);

        // Draft, not posted: only the warehouse can say what physically turned up.
        Assert.Equal(GoodsReceiptStatus.Draft, receipt.Status);

        var line = Assert.Single(receipt.Lines);
        Assert.Equal(ids.EspressoItemId, line.ItemId);
        Assert.Equal(5000m, line.Quantity);
        Assert.Equal(5000m, line.AcceptedQuantity);
        Assert.Equal(0.50m, line.UnitCost);

        // Drafting it must not move stock on its own - posting the receipt does that.
        Assert.Equal(0m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task The_drafted_receipt_can_then_be_posted_to_complete_the_invoice_only_cycle()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await SetFlagAsync(companyId, autoCreateReceipt: true);
        var invoiceId = await SeedDraftInvoiceAsync(ids, warehouseId: ids.MainWarehouseId);
        var client = CreateClient(companyId);

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        long receiptId;
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            receiptId = (await db.GoodsReceipts.SingleAsync()).Id;
        }

        (await client.PostAsync($"/api/v1/purchasing/goods-receipts/{receiptId}/post", null)).EnsureSuccessStatusCode();

        // Buy on a direct invoice, receive against it, stock lands - end to end, with the supplier
        // link intact the whole way. This is what the StockIn workaround could never express.
        Assert.Equal(5000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task Nothing_is_drafted_while_the_flag_is_off()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await SetFlagAsync(companyId, autoCreateReceipt: false);
        var invoiceId = await SeedDraftInvoiceAsync(ids, warehouseId: ids.MainWarehouseId);
        var client = CreateClient(companyId);

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Equal(0, await db.GoodsReceipts.CountAsync());
    }

    [Fact]
    public async Task Falls_back_to_the_suppliers_default_warehouse_when_the_invoice_names_none()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await SetFlagAsync(companyId, autoCreateReceipt: true);

        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            var supplier = await db.Suppliers.FirstAsync(s => s.Id == ids.SupplierId);
            supplier.DefaultWarehouseId = ids.BranchWarehouseId;
            await db.SaveChangesAsync();
        }

        var invoiceId = await SeedDraftInvoiceAsync(ids, warehouseId: null);
        var client = CreateClient(companyId);

        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var verify = factory.CreateDirectDbContext(companyId);
        var receipt = await verify.GoodsReceipts.SingleAsync();
        Assert.Equal(ids.BranchWarehouseId, receipt.WarehouseId);
    }

    [Fact]
    public async Task Posting_fails_loudly_when_no_warehouse_can_be_resolved()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await SetFlagAsync(companyId, autoCreateReceipt: true);

        // Neither the invoice nor the supplier names one.
        var invoiceId = await SeedDraftInvoiceAsync(ids, warehouseId: null);
        var client = CreateClient(companyId);

        var post = await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null);

        // Silently skipping would leave an operator who switched the flag on waiting for a receipt
        // that is never coming.
        Assert.False(post.IsSuccessStatusCode);
        Assert.Contains("PUR-INVOICE-RECEIPT-NO-WAREHOUSE", await post.Content.ReadAsStringAsync());

        await using var db = factory.CreateDirectDbContext(companyId);
        var invoice = await db.PurchaseInvoices.FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(PurchaseInvoiceStatus.Draft, invoice.Status);   // the post rolled back whole
    }
}
