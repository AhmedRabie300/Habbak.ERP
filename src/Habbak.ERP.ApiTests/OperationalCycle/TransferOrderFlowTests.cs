using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 4 of Docs/End-to-End-Cycle-Test.md — posting the transfer order takes stock out of the
/// source warehouse immediately, before the branch has received anything. That in-transit window
/// is intentional, and it is why the receipt in step 5 is a separate document.
/// </summary>
public class TransferOrderFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    [Fact]
    public async Task Posting_a_transfer_order_removes_stock_from_the_source_warehouse()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId, 20000m);
        var client = CreateClient(companyId);

        var (_, transferOrderId) = await BranchRequestFlowTests.CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);

        var post = await client.PostAsync($"/api/v1/inventory/transfer-order/{transferOrderId}/post", null);
        post.EnsureSuccessStatusCode();

        Assert.Equal(3000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
        Assert.Equal(12000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId));

        // Stock has left the source but has not arrived anywhere yet.
        Assert.Equal(0m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));

        await using var db = factory.CreateDirectDbContext(companyId);
        var order = await db.WarehouseDocuments.FirstAsync(d => d.Id == transferOrderId);
        Assert.Equal(WarehouseDocumentStatus.Posted, order.Status);

        var movements = await db.StockTransactions.Where(t => t.SourceDocumentId == transferOrderId).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m =>
        {
            Assert.Equal(TransactionType.TransferOut, m.TransactionType);
            Assert.Equal(ids.MainWarehouseId, m.WarehouseId);
            Assert.Equal("WarehouseDocument", m.SourceDocumentType);
            Assert.Null(m.JournalEntryId);   // no accounting is wired into stock movement
        });
    }

    [Fact]
    public async Task Transfer_order_cannot_overdraw_a_warehouse_that_disallows_negative_balance()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId, 100m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId, 100m);
        var client = CreateClient(companyId);

        var (_, transferOrderId) = await BranchRequestFlowTests.CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);

        var post = await client.PostAsync($"/api/v1/inventory/transfer-order/{transferOrderId}/post", null);

        Assert.False(post.IsSuccessStatusCode);
        Assert.Contains("INV-R1-NEGATIVE-BALANCE", await post.Content.ReadAsStringAsync());
        Assert.Equal(100m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
    }
}
