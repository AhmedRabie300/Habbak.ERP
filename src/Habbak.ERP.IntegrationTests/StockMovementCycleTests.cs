using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Infrastructure.Services;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// The stock engine underneath every step of Docs/End-to-End-Cycle-Test.md. These sit a layer
/// below the API flow tests on purpose: the transfer/receipt/sale documents all funnel into
/// StockMovementService, so its invariants - direction, the negative-balance guard, batch
/// tracking - are worth pinning once here rather than re-asserting per document type.
/// </summary>
public class StockMovementCycleTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private sealed record Seed(long WarehouseId, long OverdraftWarehouseId, long ItemId, long TrackedItemId);

    private async Task<Seed> SeedAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var unit = new UnitOfMeasure
        {
            CompanyId = companyId, Code = $"GM{companyId % 10000}", NameAr = "جرام", NameEn = "Gram",
            Category = UnitCategory.Weight, IsActive = true
        };
        db.UnitsOfMeasure.Add(unit);
        await db.SaveChangesAsync();

        var warehouse = new Warehouse
        {
            CompanyId = companyId, Code = $"WH{companyId % 10000}", NameAr = "مخزن", NameEn = "Warehouse",
            WarehouseType = WarehouseType.Main, AllowNegativeBalance = false, IsActive = true
        };
        var overdraft = new Warehouse
        {
            CompanyId = companyId, Code = $"WHN{companyId % 10000}", NameAr = "مخزن بالسالب", NameEn = "Overdraft",
            WarehouseType = WarehouseType.Main, AllowNegativeBalance = true, IsActive = true
        };
        db.Warehouses.AddRange(warehouse, overdraft);

        var item = new Item
        {
            CompanyId = companyId, Code = $"ITM{companyId % 10000}", NameAr = "بن", NameEn = "Coffee",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = unit.Id, IsStocked = true, Status = ItemStatus.Active
        };
        var tracked = new Item
        {
            CompanyId = companyId, Code = $"TRK{companyId % 10000}", NameAr = "لبن متتبَّع", NameEn = "Tracked Milk",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = unit.Id, IsStocked = true, IsTracked = true,
            Status = ItemStatus.Active
        };
        db.Items.AddRange(item, tracked);
        await db.SaveChangesAsync();

        return new Seed(warehouse.Id, overdraft.Id, item.Id, tracked.Id);
    }

    private async Task ApplyAsync(long companyId, long warehouseId, long itemId, TransactionType type, decimal quantity, string? batch = null)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var service = new StockMovementService(db);
        await service.ApplyMovementAsync(new StockMovementRequest
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ItemId = itemId,
            TransactionType = type,
            Quantity = quantity,
            UnitCost = 1m,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            SourceDocumentType = "Test",
            SourceDocumentId = 1,
            BatchNumber = batch
        }, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private async Task<decimal> BalanceAsync(long companyId, long warehouseId, long itemId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var row = await db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        return row?.QuantityOnHand ?? 0m;
    }

    [Fact]
    public async Task Inbound_and_outbound_types_move_the_balance_in_opposite_directions()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 1000m);
        Assert.Equal(1000m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));

        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.TransferOut, 400m);
        Assert.Equal(600m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));

        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.TransferIn, 150m);
        Assert.Equal(750m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));

        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.POSSale, 50m);
        Assert.Equal(700m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));
    }

    [Fact]
    public async Task Quantity_is_always_stored_positive_with_direction_carried_by_the_type()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m);
        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.TransferOut, 30m);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var movements = await db.StockTransactions.Where(t => t.ItemId == seed.ItemId).ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.True(m.Quantity > 0));
        Assert.All(movements, m => Assert.Null(m.JournalEntryId));
    }

    [Fact]
    public async Task Outbound_below_zero_is_blocked_unless_the_warehouse_allows_it()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);
        await ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 10m);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ApplyAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.TransferOut, 25m));
        Assert.Equal("INV-R1-NEGATIVE-BALANCE", error.Code);
        Assert.Equal(10m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));

        // The same movement is permitted where the warehouse is explicitly configured for it.
        await ApplyAsync(companyId, seed.OverdraftWarehouseId, seed.ItemId, TransactionType.TransferOut, 25m);
        Assert.Equal(-25m, await BalanceAsync(companyId, seed.OverdraftWarehouseId, seed.ItemId));
    }

    // ---------------------------------------------------------------- cost (rules 39 and 41)

    private async Task<(decimal AverageCost, DateTime? LastUpdate)> CostAsync(long companyId, long warehouseId, long itemId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var row = await db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        return (row?.AverageCost ?? 0m, row?.LastCostUpdateAtUtc);
    }

    private async Task ApplyCostedAsync(long companyId, long warehouseId, long itemId, TransactionType type, decimal quantity, decimal unitCost)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var service = new StockMovementService(db);
        await service.ApplyMovementAsync(new StockMovementRequest
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ItemId = itemId,
            TransactionType = type,
            Quantity = quantity,
            UnitCost = unitCost,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            SourceDocumentType = "Test",
            SourceDocumentId = 1
        }, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task First_receipt_sets_the_average_cost_outright()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 7.5m);

        var (cost, lastUpdate) = await CostAsync(companyId, seed.WarehouseId, seed.ItemId);
        Assert.Equal(7.5m, cost);
        Assert.NotNull(lastUpdate);
    }

    [Fact]
    public async Task Second_receipt_at_a_different_price_produces_the_weighted_average()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 10m);
        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 300m, 20m);

        // (100x10 + 300x20) / 400 = 17.50
        var (cost, _) = await CostAsync(companyId, seed.WarehouseId, seed.ItemId);
        Assert.Equal(17.5m, cost);
    }

    [Fact]
    public async Task Issuing_stock_does_not_disturb_the_average()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 10m);
        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.TransferOut, 40m, 999m);

        var (cost, _) = await CostAsync(companyId, seed.WarehouseId, seed.ItemId);
        Assert.Equal(10m, cost);
    }

    [Fact]
    public async Task An_outbound_movement_is_costed_from_the_balance_not_from_the_caller()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 10m);

        // The caller's figure is deliberately absurd: COGS must come from what the stock is worth,
        // never from whatever the calling module happened to pass.
        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.POSSale, 5m, 999m);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var sale = await db.StockTransactions.SingleAsync(t => t.TransactionType == TransactionType.POSSale);
        Assert.Equal(10m, sale.UnitCost);
    }

    [Fact]
    public async Task A_receipt_without_a_real_unit_cost_is_refused()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        // Rule 41 - this silent zero is exactly how cost used to vanish on transfer.
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 0m));

        Assert.Equal("INV-R41-UNIT-COST-REQUIRED", error.Code);
        Assert.Equal(0m, await BalanceAsync(companyId, seed.WarehouseId, seed.ItemId));
    }

    [Fact]
    public async Task Average_cost_is_tracked_per_warehouse_not_per_item()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 10m);
        await ApplyCostedAsync(companyId, seed.OverdraftWarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 40m);

        // Same item, two warehouses, genuinely different costs - the whole reason this lives on
        // StockBalance rather than Item.
        Assert.Equal(10m, (await CostAsync(companyId, seed.WarehouseId, seed.ItemId)).AverageCost);
        Assert.Equal(40m, (await CostAsync(companyId, seed.OverdraftWarehouseId, seed.ItemId)).AverageCost);
    }

    [Fact]
    public async Task Inbound_cost_resolution_prefers_the_warehouse_average_then_standard_cost()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var service = new StockMovementService(db);

            // Nothing received yet and no standard cost: refusing beats inventing a zero.
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.ResolveInboundCostAsync(seed.ItemId, seed.WarehouseId));
            Assert.Equal("INV-R41-UNIT-COST-REQUIRED", error.Code);

            var item = await db.Items.FirstAsync(i => i.Id == seed.ItemId);
            item.StandardCost = 3m;
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            Assert.Equal(3m, await new StockMovementService(db).ResolveInboundCostAsync(seed.ItemId, seed.WarehouseId));
        }

        await ApplyCostedAsync(companyId, seed.WarehouseId, seed.ItemId, TransactionType.Purchase, 100m, 12m);

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            // A real average now exists, so it wins over the estimate.
            Assert.Equal(12m, await new StockMovementService(db).ResolveInboundCostAsync(seed.ItemId, seed.WarehouseId));
        }
    }

    [Fact]
    public async Task Tracked_items_require_a_batch_number_on_every_movement()
    {
        var companyId = NewCompanyId();
        var seed = await SeedAsync(companyId);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            ApplyAsync(companyId, seed.WarehouseId, seed.TrackedItemId, TransactionType.Purchase, 5m));
        Assert.Equal("INV-R8-BATCH-REQUIRED", error.Code);

        await ApplyAsync(companyId, seed.WarehouseId, seed.TrackedItemId, TransactionType.Purchase, 5m, batch: "B-001");
        Assert.Equal(5m, await BalanceAsync(companyId, seed.WarehouseId, seed.TrackedItemId));
    }
}
