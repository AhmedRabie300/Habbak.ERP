using System.Net.Http.Json;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 5 of Docs/End-to-End-Cycle-Test.md — the branch receives what the main warehouse sent.
/// Together with TransferOrderFlowTests this covers the conservation property that matters most
/// for inventory integrity: nothing is created or destroyed by moving it between warehouses.
/// </summary>
public class TransferReceiptFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    internal static async Task<long> ReceiveAsync(HttpClient client, CycleIds ids, long transferOrderId, decimal espressoQty, decimal milkQty)
    {
        var create = await client.PostAsJsonAsync("/api/v1/inventory/transfer-receipt", new
        {
            relatedWarehouseDocumentId = transferOrderId,
            branchId = ids.BranchId,
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            destinationWarehouseId = ids.BranchWarehouseId,
            lines = new[]
            {
                new { itemId = ids.EspressoItemId, quantity = espressoQty },
                new { itemId = ids.MilkItemId,     quantity = milkQty }
            }
        });
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    [Fact]
    public async Task Full_transfer_chain_conserves_total_quantity_across_warehouses()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId, 20000m);
        var client = CreateClient(companyId);

        var (_, transferOrderId) = await BranchRequestFlowTests.CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);
        (await client.PostAsync($"/api/v1/inventory/transfer-order/{transferOrderId}/post", null)).EnsureSuccessStatusCode();

        var receiptId = await ReceiveAsync(client, ids, transferOrderId, 2000m, 8000m);
        (await client.PostAsync($"/api/v1/inventory/transfer-receipt/{receiptId}/post", null)).EnsureSuccessStatusCode();

        var mainEspresso = await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId);
        var branchEspresso = await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId);
        var mainMilk = await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId);
        var branchMilk = await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId);

        Assert.Equal(3000m, mainEspresso);
        Assert.Equal(2000m, branchEspresso);
        Assert.Equal(5000m, mainEspresso + branchEspresso);

        Assert.Equal(12000m, mainMilk);
        Assert.Equal(8000m, branchMilk);
        Assert.Equal(20000m, mainMilk + branchMilk);

        await using var db = factory.CreateDirectDbContext(companyId);
        var receipt = await db.WarehouseDocuments.FirstAsync(d => d.Id == receiptId);
        Assert.Equal(WarehouseDocumentType.TransferReceipt, receipt.DocumentType);
        Assert.Equal(WarehouseDocumentStatus.Posted, receipt.Status);
        Assert.Equal(transferOrderId, receipt.RelatedWarehouseDocumentId);

        // The custody officer travels with the goods rather than being re-entered at the branch.
        Assert.Equal(ids.CustodyOfficerId, receipt.CustodyOfficerId);

        var inbound = await db.StockTransactions.Where(t => t.SourceDocumentId == receiptId).ToListAsync();
        Assert.All(inbound, m => Assert.Equal(TransactionType.TransferIn, m.TransactionType));
    }

    [Fact]
    public async Task Transferred_stock_carries_its_cost_to_the_destination()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        // Buy into the main warehouse at a real price, through the document flow so the weighted
        // average is established the way it would be in production.
        var stockIn = await client.PostAsJsonAsync("/api/v1/inventory/stock-in", new
        {
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            destinationWarehouseId = ids.MainWarehouseId,
            lines = new[]
            {
                new { itemId = ids.EspressoItemId, quantity = 5000m, unitCost = 0.50m },
                new { itemId = ids.MilkItemId,     quantity = 20000m, unitCost = 0.02m }
            }
        });
        stockIn.EnsureSuccessStatusCode();
        var stockInId = (await stockIn.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        (await client.PostAsync($"/api/v1/inventory/stock-in/{stockInId}/post", null)).EnsureSuccessStatusCode();

        var (_, transferOrderId) = await BranchRequestFlowTests.CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);
        (await client.PostAsync($"/api/v1/inventory/transfer-order/{transferOrderId}/post", null)).EnsureSuccessStatusCode();

        var receiptId = await ReceiveAsync(client, ids, transferOrderId, 2000m, 8000m);
        (await client.PostAsync($"/api/v1/inventory/transfer-receipt/{receiptId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);

        // Rule 40: the destination is worth exactly what the source was, not zero and not a
        // separately derived figure. Losing this on transfer was gap G-7.
        var branchEspresso = await db.StockBalances.FirstAsync(b => b.WarehouseId == ids.BranchWarehouseId && b.ItemId == ids.EspressoItemId);
        var branchMilk = await db.StockBalances.FirstAsync(b => b.WarehouseId == ids.BranchWarehouseId && b.ItemId == ids.MilkItemId);
        Assert.Equal(0.50m, branchEspresso.AverageCost);
        Assert.Equal(0.02m, branchMilk.AverageCost);

        // And the movements themselves carry it, so valuation and COGS have something to read.
        var transferMovements = await db.StockTransactions
            .Where(t => t.SourceDocumentId == transferOrderId || t.SourceDocumentId == receiptId)
            .ToListAsync();
        Assert.NotEmpty(transferMovements);
        Assert.All(transferMovements, m => Assert.True(m.UnitCost > 0, "a transfer movement was recorded with no cost"));
    }

    [Fact]
    public async Task Receipt_is_rejected_when_the_transfer_order_has_not_been_posted()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId, 20000m);
        var client = CreateClient(companyId);

        var (_, transferOrderId) = await BranchRequestFlowTests.CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);

        // Deliberately skipping the post step: goods that never left cannot arrive.
        var create = await client.PostAsJsonAsync("/api/v1/inventory/transfer-receipt", new
        {
            relatedWarehouseDocumentId = transferOrderId,
            branchId = ids.BranchId,
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            destinationWarehouseId = ids.BranchWarehouseId,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2000m } }
        });

        Assert.False(create.IsSuccessStatusCode);
        Assert.Contains("INV-R37-TRANSFER-ORDER-NOT-POSTED", await create.Content.ReadAsStringAsync());
    }

    private sealed record CreatedResponse(long Id);
}
