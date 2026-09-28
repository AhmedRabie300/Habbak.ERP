using System.Net.Http.Json;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Rule 42 (02-Module-Inventory-Manufacturing.md): an invoice line carries the cost of fulfilling
/// it, frozen at the moment of sale. The posting engine's `SumLineQuantityTimesUnitCost` reads this
/// and recalculates nothing, so COGS is only ever as correct as what gets written here.
/// </summary>
public class InvoiceLineCostSnapshotTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
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
    public async Task A_real_time_sale_costs_the_line_at_what_its_recipe_consumed()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m, averageCost: 0.50m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, 8000m, averageCost: 0.02m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var line = (await db.POSInvoices.Include(i => i.Lines).SingleAsync()).Lines.Single();

        // 9g espresso at 0.50 + 150ml milk at 0.02 = 4.50 + 3.00 = 7.50 per latte.
        // The latte's own average is zero (never stocked), so anything reading that would have
        // reported this sale as costing nothing.
        Assert.Equal(7.50m, line.UnitCost);
        Assert.Equal(40m, line.UnitPrice);
    }

    [Fact]
    public async Task Component_cost_scales_with_the_quantity_sold()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m, averageCost: 0.50m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, 8000m, averageCost: 0.02m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids, quantity: 4m);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 160m)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var line = (await db.POSInvoices.Include(i => i.Lines).SingleAsync()).Lines.Single();

        // UnitCost stays per-unit however many were sold - it is a rate, not a total.
        Assert.Equal(7.50m, line.UnitCost);
        Assert.Equal(30m, line.Quantity * line.UnitCost);
    }

    [Fact]
    public async Task A_stocked_sale_costs_the_line_at_the_items_own_average()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m, averageCost: 0.50m);
        var client = CreateClient(companyId);

        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            var item = await db.Items.FirstAsync(i => i.Id == ids.EspressoItemId);
            item.IsSellable = true;
            item.DefaultPrice = 25m;
            await db.SaveChangesAsync();
        }

        var checkId = await POSTerminalSaleFlowTests.OpenCheckAsync(client, ids);
        await POSTerminalSaleFlowTests.AddLineAsync(client, checkId, ids.EspressoItemId, quantity: 10m);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 250m)).EnsureSuccessStatusCode();

        await using var verify = factory.CreateDirectDbContext(companyId);
        var line = (await verify.POSInvoices.Include(i => i.Lines).SingleAsync()).Lines.Single();
        Assert.Equal(0.50m, line.UnitCost);
    }

    [Fact]
    public async Task The_frozen_cost_survives_a_later_change_to_the_warehouse_average()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m, averageCost: 0.50m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, 8000m, averageCost: 0.02m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        // Coffee doubles in price afterwards.
        var stockIn = await client.PostAsJsonAsync("/api/v1/inventory/stock-in", new
        {
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            destinationWarehouseId = ids.BranchWarehouseId,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2000m, unitCost = 1.50m } }
        });
        stockIn.EnsureSuccessStatusCode();
        var stockInId = (await stockIn.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        (await client.PostAsync($"/api/v1/inventory/stock-in/{stockInId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);

        var balance = await db.StockBalances.FirstAsync(b => b.WarehouseId == ids.BranchWarehouseId && b.ItemId == ids.EspressoItemId);
        Assert.True(balance.AverageCost > 0.50m, "the warehouse average should have moved");

        // The invoice that was already issued must not have moved with it.
        var line = (await db.POSInvoices.Include(i => i.Lines).SingleAsync()).Lines.Single();
        Assert.Equal(7.50m, line.UnitCost);
    }

    [Fact]
    public async Task Total_cost_of_sale_can_be_read_straight_off_the_invoice_lines()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m, averageCost: 0.50m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, 8000m, averageCost: 0.02m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids, quantity: 2m);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 80m)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var lines = (await db.POSInvoices.Include(i => i.Lines).SingleAsync()).Lines.ToList();

        // This is exactly what the posting engine's SumLineQuantityTimesUnitCost evaluates.
        var cogs = lines.Sum(l => l.Quantity * l.UnitCost);
        Assert.Equal(15m, cogs);

        // And it reconciles with what actually left the warehouse.
        var issued = await db.StockTransactions
            .Where(t => t.SourceDocumentType == "POSSale")
            .SumAsync(t => t.Quantity * t.UnitCost);
        Assert.Equal(cogs, issued);
    }

    private sealed record CreatedResponse(long Id);
}
