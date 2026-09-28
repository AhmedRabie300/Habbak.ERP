using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Reports.Queries;

// ---- Report #2: تقرير المخزون (Stock Report) — ثابت ----

public sealed class StockReportRowDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required decimal QuantityOnHand { get; init; }
    public decimal? StandardCost { get; init; }
    public required decimal EstimatedValue { get; init; }
}

/// <summary>Only rows with QuantityOnHand != 0 are worth listing — a zero-balance StockBalance row
/// exists for every item/warehouse pair that ever moved and would otherwise flood the report.</summary>
public sealed record GetStockReportQuery : IRequest<IReadOnlyList<StockReportRowDto>>;

public sealed class GetStockReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetStockReportQuery, IReadOnlyList<StockReportRowDto>>
{
    public async Task<IReadOnlyList<StockReportRowDto>> Handle(GetStockReportQuery request, CancellationToken cancellationToken) =>
        await db.StockBalances
            .AsNoTracking()
            .Include(b => b.Item)
            .Include(b => b.Warehouse)
            .Where(b => b.QuantityOnHand != 0)
            .Select(b => new StockReportRowDto
            {
                ItemId = b.ItemId,
                ItemCode = b.Item!.Code,
                ItemNameAr = b.Item!.NameAr,
                WarehouseId = b.WarehouseId,
                WarehouseNameAr = b.Warehouse!.NameAr,
                QuantityOnHand = b.QuantityOnHand,
                StandardCost = b.Item!.StandardCost,
                // Valued at the warehouse's actual weighted average (rule 39), not the item's
                // standard cost — StandardCost is an estimate kept for variance comparison and is
                // unset on almost every item, which is what made this report read as zero.
                EstimatedValue = b.QuantityOnHand * b.AverageCost
            })
            .OrderBy(r => r.ItemNameAr)
            .ThenBy(r => r.WarehouseNameAr)
            .ToListAsync(cancellationToken);
}

// ---- Report #3: الأصناف تحت الحد الأدنى (Below-Minimum Items) — ثابت ----

public sealed class BelowMinimumItemRowDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required long WarehouseId { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required decimal QuantityOnHand { get; init; }
    public required decimal MinStockLevel { get; init; }
    public decimal? ReorderPoint { get; init; }
    public required decimal ShortageQuantity { get; init; }
}

public sealed record GetBelowMinimumItemsReportQuery : IRequest<IReadOnlyList<BelowMinimumItemRowDto>>;

public sealed class GetBelowMinimumItemsReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBelowMinimumItemsReportQuery, IReadOnlyList<BelowMinimumItemRowDto>>
{
    public async Task<IReadOnlyList<BelowMinimumItemRowDto>> Handle(GetBelowMinimumItemsReportQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.ItemWarehouseSettings
            .AsNoTracking()
            .Include(s => s.Item)
            .Include(s => s.Warehouse)
            .Where(s => s.MinStockLevel != null)
            .Select(s => new { s.ItemId, s.WarehouseId, s.Item!.Code, s.Item!.NameAr, WarehouseNameAr = s.Warehouse!.NameAr, s.MinStockLevel, s.ReorderPoint })
            .ToListAsync(cancellationToken);

        var balances = await db.StockBalances
            .AsNoTracking()
            .Select(b => new { b.ItemId, b.WarehouseId, b.QuantityOnHand })
            .ToListAsync(cancellationToken);
        var balanceLookup = balances.ToDictionary(b => (b.ItemId, b.WarehouseId), b => b.QuantityOnHand);

        return rows
            .Select(r => new
            {
                r,
                QuantityOnHand = balanceLookup.GetValueOrDefault((r.ItemId, r.WarehouseId))
            })
            .Where(x => x.QuantityOnHand < x.r.MinStockLevel!.Value)
            .Select(x => new BelowMinimumItemRowDto
            {
                ItemId = x.r.ItemId,
                ItemCode = x.r.Code,
                ItemNameAr = x.r.NameAr,
                WarehouseId = x.r.WarehouseId,
                WarehouseNameAr = x.r.WarehouseNameAr,
                QuantityOnHand = x.QuantityOnHand,
                MinStockLevel = x.r.MinStockLevel!.Value,
                ReorderPoint = x.r.ReorderPoint,
                ShortageQuantity = x.r.MinStockLevel!.Value - x.QuantityOnHand
            })
            .OrderByDescending(r => r.ShortageQuantity)
            .ToList();
    }
}
