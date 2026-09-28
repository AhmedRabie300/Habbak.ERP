using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Reports.Queries;

// ---- Report #4: تقرير الجرد (Inventory Count) — ثابت ----

public sealed class InventoryCountReportRowDto
{
    public required long Id { get; init; }
    public required string CountNumber { get; init; }
    public required DateOnly CountDate { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required string CountType { get; init; }
    public required string Status { get; init; }
    public required int LineCount { get; init; }
    public required int VarianceLineCount { get; init; }
    public required decimal NetVarianceQuantity { get; init; }
}

public sealed record GetInventoryCountsReportQuery : IRequest<IReadOnlyList<InventoryCountReportRowDto>>;

public sealed class GetInventoryCountsReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInventoryCountsReportQuery, IReadOnlyList<InventoryCountReportRowDto>>
{
    public async Task<IReadOnlyList<InventoryCountReportRowDto>> Handle(GetInventoryCountsReportQuery request, CancellationToken cancellationToken) =>
        await db.InventoryCounts
            .AsNoTracking()
            .Include(c => c.Warehouse)
            .Include(c => c.Lines)
            .OrderByDescending(c => c.CountDate)
            .Select(c => new InventoryCountReportRowDto
            {
                Id = c.Id,
                CountNumber = c.CountNumber,
                CountDate = c.CountDate,
                WarehouseNameAr = c.Warehouse!.NameAr,
                CountType = c.CountType.ToString(),
                Status = c.Status.ToString(),
                LineCount = c.Lines.Count,
                VarianceLineCount = c.Lines.Count(l => l.VarianceQuantity != null && l.VarianceQuantity != 0),
                NetVarianceQuantity = c.Lines.Sum(l => l.VarianceQuantity ?? 0)
            })
            .ToListAsync(cancellationToken);
}

// ---- Report #7: تقرير الهالك (Waste) — ثابت ----

public sealed class WasteReportRowDto
{
    public required long Id { get; init; }
    public required DateOnly WasteDate { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required string WarehouseNameAr { get; init; }
    public required decimal Quantity { get; init; }
    public decimal? StandardCost { get; init; }
    public required decimal EstimatedValue { get; init; }
    public required string Reason { get; init; }
    public string? SourceDocumentType { get; init; }
}

public sealed record GetWasteReportQuery : IRequest<IReadOnlyList<WasteReportRowDto>>;

public sealed class GetWasteReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWasteReportQuery, IReadOnlyList<WasteReportRowDto>>
{
    public async Task<IReadOnlyList<WasteReportRowDto>> Handle(GetWasteReportQuery request, CancellationToken cancellationToken) =>
        await db.WasteRecords
            .AsNoTracking()
            .Include(w => w.Item)
            .Include(w => w.Warehouse)
            .OrderByDescending(w => w.WasteDate)
            .Select(w => new WasteReportRowDto
            {
                Id = w.Id,
                WasteDate = w.WasteDate,
                ItemCode = w.Item!.Code,
                ItemNameAr = w.Item!.NameAr,
                WarehouseNameAr = w.Warehouse!.NameAr,
                Quantity = w.Quantity,
                StandardCost = w.Item!.StandardCost,
                EstimatedValue = w.Quantity * (w.Item!.StandardCost ?? 0),
                Reason = w.Reason,
                SourceDocumentType = w.SourceDocumentType
            })
            .ToListAsync(cancellationToken);
}
