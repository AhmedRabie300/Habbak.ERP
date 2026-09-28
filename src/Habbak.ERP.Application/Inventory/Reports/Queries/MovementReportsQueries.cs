using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Reports.Queries;

// ---- Report #1: حركة صنف (Item Movement) — تفاعلي ----

public sealed class ItemMovementRowDto
{
    public required long Id { get; init; }
    public required DateOnly TransactionDate { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required string TransactionType { get; init; }
    public required bool IsInbound { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public string? BatchNumber { get; init; }
}

public sealed record GetItemMovementReportQuery(long ItemId, long? WarehouseId, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<ItemMovementRowDto>>;

public sealed class GetItemMovementReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetItemMovementReportQuery, IReadOnlyList<ItemMovementRowDto>>
{
    public async Task<IReadOnlyList<ItemMovementRowDto>> Handle(GetItemMovementReportQuery request, CancellationToken cancellationToken) =>
        await db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Where(t => t.ItemId == request.ItemId
                && t.TransactionDate >= request.From && t.TransactionDate <= request.To
                && (request.WarehouseId == null || t.WarehouseId == request.WarehouseId))
            .OrderBy(t => t.TransactionDate)
            .Select(t => new ItemMovementRowDto
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                WarehouseNameAr = t.Warehouse!.NameAr,
                TransactionType = t.TransactionType.ToString(),
                IsInbound = TransactionTypeExtensions.IsInbound(t.TransactionType),
                Quantity = t.Quantity,
                UnitCost = t.UnitCost,
                BatchNumber = t.BatchNumber
            })
            .ToListAsync(cancellationToken);
}

// ---- Report #5: تقرير الحركة اليومية (Daily Movement) — تفاعلي ----

public sealed class DailyMovementRowDto
{
    public required long Id { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required string TransactionType { get; init; }
    public required bool IsInbound { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
}

public sealed record GetDailyMovementReportQuery(DateOnly Date) : IRequest<IReadOnlyList<DailyMovementRowDto>>;

public sealed class GetDailyMovementReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDailyMovementReportQuery, IReadOnlyList<DailyMovementRowDto>>
{
    public async Task<IReadOnlyList<DailyMovementRowDto>> Handle(GetDailyMovementReportQuery request, CancellationToken cancellationToken) =>
        await db.StockTransactions
            .AsNoTracking()
            .Include(t => t.Item)
            .Include(t => t.Warehouse)
            .Where(t => t.TransactionDate == request.Date)
            .OrderBy(t => t.Item!.NameAr)
            .Select(t => new DailyMovementRowDto
            {
                Id = t.Id,
                ItemCode = t.Item!.Code,
                ItemNameAr = t.Item!.NameAr,
                WarehouseNameAr = t.Warehouse!.NameAr,
                TransactionType = t.TransactionType.ToString(),
                IsInbound = TransactionTypeExtensions.IsInbound(t.TransactionType),
                Quantity = t.Quantity,
                UnitCost = t.UnitCost
            })
            .ToListAsync(cancellationToken);
}

// ---- Report #6: تقرير استهلاك المواد الخام (Raw Material Consumption) — تفاعلي ----

public sealed class RawMaterialConsumptionRowDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal TotalQuantityConsumed { get; init; }
    public required decimal TotalValue { get; init; }
}

/// <summary>Consumption = ProductionIssue-type stock transactions against RawMaterial items —
/// these are auto-posted by CompleteProductionOrderCommand (ProductionOrder's own class doc), so
/// this is the closed set of "raw material actually left the warehouse for production" events,
/// distinct from POSSale/Waste/TransferOut which move other item types or for other reasons.</summary>
public sealed record GetRawMaterialConsumptionReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<RawMaterialConsumptionRowDto>>;

public sealed class GetRawMaterialConsumptionReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRawMaterialConsumptionReportQuery, IReadOnlyList<RawMaterialConsumptionRowDto>>
{
    public async Task<IReadOnlyList<RawMaterialConsumptionRowDto>> Handle(GetRawMaterialConsumptionReportQuery request, CancellationToken cancellationToken) =>
        await db.StockTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.ProductionIssue
                && t.Item!.ItemType == ItemType.RawMaterial
                && t.TransactionDate >= request.From && t.TransactionDate <= request.To)
            .GroupBy(t => new { t.ItemId, t.Item!.Code, t.Item!.NameAr })
            .Select(g => new RawMaterialConsumptionRowDto
            {
                ItemId = g.Key.ItemId,
                ItemCode = g.Key.Code,
                ItemNameAr = g.Key.NameAr,
                TotalQuantityConsumed = g.Sum(t => t.Quantity),
                TotalValue = g.Sum(t => t.Quantity * t.UnitCost)
            })
            .OrderByDescending(r => r.TotalValue)
            .ToListAsync(cancellationToken);
}

// ---- Report #8: تقرير تحويلات المخزون (Stock Transfers) — تفاعلي ----

public sealed class StockTransferRowDto
{
    public required long Id { get; init; }
    public required string DocumentNumber { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required string DocumentType { get; init; }
    public string? SourceWarehouseNameAr { get; init; }
    public string? DestinationWarehouseNameAr { get; init; }
    public required string Status { get; init; }
    public string? CustodyOfficerNameAr { get; init; }
    public required decimal TotalQuantity { get; init; }
}

public sealed record GetStockTransfersReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<StockTransferRowDto>>;

public sealed class GetStockTransfersReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetStockTransfersReportQuery, IReadOnlyList<StockTransferRowDto>>
{
    private static readonly WarehouseDocumentType[] TransferTypes =
    [
        WarehouseDocumentType.TransferOrder, WarehouseDocumentType.TransferReceipt
    ];

    public async Task<IReadOnlyList<StockTransferRowDto>> Handle(GetStockTransfersReportQuery request, CancellationToken cancellationToken) =>
        await db.WarehouseDocuments
            .AsNoTracking()
            .Include(d => d.SourceWarehouse)
            .Include(d => d.DestinationWarehouse)
            .Include(d => d.CustodyOfficer)
            .Where(d => TransferTypes.Contains(d.DocumentType) && d.DocumentDate >= request.From && d.DocumentDate <= request.To)
            .OrderByDescending(d => d.DocumentDate)
            .Select(d => new StockTransferRowDto
            {
                Id = d.Id,
                DocumentNumber = d.DocumentNumber,
                DocumentDate = d.DocumentDate,
                DocumentType = d.DocumentType.ToString(),
                SourceWarehouseNameAr = d.SourceWarehouse == null ? null : d.SourceWarehouse.NameAr,
                DestinationWarehouseNameAr = d.DestinationWarehouse == null ? null : d.DestinationWarehouse.NameAr,
                Status = d.Status.ToString(),
                CustodyOfficerNameAr = d.CustodyOfficer == null ? null : d.CustodyOfficer.NameAr,
                TotalQuantity = d.Lines.Sum(l => l.Quantity)
            })
            .ToListAsync(cancellationToken);
}
