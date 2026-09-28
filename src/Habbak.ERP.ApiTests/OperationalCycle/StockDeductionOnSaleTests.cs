using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 7 of Docs/End-to-End-Cycle-Test.md — what a POS sale takes out of the warehouse.
///
/// Covers both halves of rule 17/18 (03-Inventory-Module.md): the real-time model consumes the
/// sold item's recipe components and refuses the sale when any of them is short, while the stocked
/// model deducts the finished item itself. The stocked case is also the configured default, so the
/// "unchanged behaviour" tests here are what guarantee that turning this feature on stays an
/// explicit decision rather than something that silently rewrites how every till behaves.
/// </summary>
public class StockDeductionOnSaleTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<(CycleIds Ids, long CompanyId, HttpClient Client)> ArrangeAsync(
        decimal espressoStock = 2000m, decimal milkStock = 8000m)
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, espressoStock);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, milkStock);
        return (ids, companyId, CreateClient(companyId));
    }

    // ---------------------------------------------------------------- real-time model

    [Fact]
    public async Task Real_time_sale_consumes_the_recipe_components_not_the_finished_item()
    {
        var (ids, companyId, client) = await ArrangeAsync();
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        Assert.Equal(2000m - CycleSeed.EspressoPerLatteGrams, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
        Assert.Equal(8000m - CycleSeed.MilkPerLatteMillilitres, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));

        // The latte itself is never stocked, so it must not be deducted.
        Assert.Equal(0m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.LatteItemId));

        await using var db = factory.CreateDirectDbContext(companyId);
        var movements = await db.StockTransactions.Where(t => t.SourceDocumentType == "POSSale").ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(TransactionType.ProductionIssue, m.TransactionType));
        Assert.All(movements, m => Assert.Null(m.JournalEntryId));
        Assert.DoesNotContain(movements, m => m.ItemId == ids.LatteItemId);
    }

    [Fact]
    public async Task Component_quantities_scale_with_the_quantity_sold()
    {
        var (ids, companyId, client) = await ArrangeAsync();
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids, quantity: 3m);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 120m)).EnsureSuccessStatusCode();

        Assert.Equal(2000m - (3 * CycleSeed.EspressoPerLatteGrams), await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
        Assert.Equal(8000m - (3 * CycleSeed.MilkPerLatteMillilitres), await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));
    }

    // ---------------------------------------------------------------- rule 18

    [Fact]
    public async Task Sale_is_refused_when_a_component_is_short_and_nothing_is_deducted()
    {
        // Enough espresso for the shot, nowhere near enough milk.
        var (ids, companyId, client) = await ArrangeAsync(espressoStock: 2000m, milkStock: 10m);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m);

        Assert.False(pay.IsSuccessStatusCode);
        var body = await pay.Content.ReadAsStringAsync();
        Assert.Contains("POS-COMPONENT-SHORTAGE", body);
        Assert.Contains("لبن", body);   // names the ingredient that ran out

        // The shortage is caught before any movement, so the espresso that *was* available
        // must not have been consumed into a sale that never completed.
        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
        Assert.Equal(10m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));
    }

    [Fact]
    public async Task Shortage_is_judged_on_the_whole_check_not_line_by_line()
    {
        // 200ml covers one latte but not two, and the two lattes sit on separate check lines.
        var (ids, companyId, client) = await ArrangeAsync(espressoStock: 2000m, milkStock: 200m);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        await POSTerminalSaleFlowTests.AddLatteLineAsync(client, ids, checkId, quantity: 1m);

        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 80m);

        Assert.False(pay.IsSuccessStatusCode);
        Assert.Contains("POS-COMPONENT-SHORTAGE", await pay.Content.ReadAsStringAsync());
        Assert.Equal(200m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));
    }

    // ---------------------------------------------------------------- rule 17 priority

    [Fact]
    public async Task Item_scope_overrides_company_scope()
    {
        var (ids, companyId, client) = await ArrangeAsync();
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Item, ids.LatteItemId, ProductionSalesMode.Stocked);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        // Item scope wins, so the stocked path applies and the components are left alone.
        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
        Assert.Equal(8000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));
    }

    // ---------------------------------------------------------------- stocked model / default

    [Fact]
    public async Task With_no_setting_configured_behaviour_is_unchanged()
    {
        var (ids, companyId, client) = await ArrangeAsync();

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        // Default is Stocked, and a made-to-order finished good is not stocked, so nothing moves.
        // This is the safety property: installing this feature does not change any existing till
        // until someone configures it.
        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
        Assert.Equal(8000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId));

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Empty(await db.StockTransactions.Where(t => t.SourceDocumentType == "POSSale").ToListAsync());
    }

    [Fact]
    public async Task Real_time_item_without_a_recipe_falls_back_to_deducting_itself()
    {
        var (ids, companyId, client) = await ArrangeAsync();
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);

        // The espresso is a stocked raw material with no recipe of its own - think a bottled drink
        // sold straight off the shelf at the same till.
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            var item = await db.Items.FirstAsync(i => i.Id == ids.EspressoItemId);
            item.IsSellable = true;
            item.DefaultPrice = 25m;
            await db.SaveChangesAsync();
        }

        var checkId = await POSTerminalSaleFlowTests.OpenCheckAsync(client, ids);
        await POSTerminalSaleFlowTests.AddLineAsync(client, checkId, ids.EspressoItemId, quantity: 4m);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 100m)).EnsureSuccessStatusCode();

        Assert.Equal(1996m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));

        await using var verify = factory.CreateDirectDbContext(companyId);
        var movement = await verify.StockTransactions.SingleAsync(t => t.SourceDocumentType == "POSSale");
        Assert.Equal(TransactionType.POSSale, movement.TransactionType);
    }
}
