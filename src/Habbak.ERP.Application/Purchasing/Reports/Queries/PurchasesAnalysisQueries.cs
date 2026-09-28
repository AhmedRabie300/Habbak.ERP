using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Reports.Queries;

// ---- Report #1: مشتريات حسب المورد (Purchases by Supplier) — تفاعلي ----

public sealed class PurchasesBySupplierRowDto
{
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required string CurrencyCode { get; init; }
    public required int InvoiceCount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal TotalPaid { get; init; }
}

/// <summary>Only counts invoices that actually posted (Posted/PartiallyPaid/Paid/Overdue) — a
/// Draft/Rejected/Cancelled invoice was never a real purchase.</summary>
public sealed record GetPurchasesBySupplierReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<PurchasesBySupplierRowDto>>;

public sealed class GetPurchasesBySupplierReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchasesBySupplierReportQuery, IReadOnlyList<PurchasesBySupplierRowDto>>
{
    private static readonly PurchaseInvoiceStatus[] PostedStatuses =
    [
        PurchaseInvoiceStatus.Posted, PurchaseInvoiceStatus.PartiallyPaid, PurchaseInvoiceStatus.Paid, PurchaseInvoiceStatus.Overdue
    ];

    public async Task<IReadOnlyList<PurchasesBySupplierRowDto>> Handle(GetPurchasesBySupplierReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseInvoices
            .AsNoTracking()
            .Where(i => PostedStatuses.Contains(i.Status) && i.InvoiceDate >= request.From && i.InvoiceDate <= request.To)
            // Grouped by currency too — TotalAmount/AmountPaid are face values in the invoice's
            // own currency with no base-currency-equivalent field to convert through, so blending
            // invoices across currencies into one sum would silently produce a meaningless total.
            .GroupBy(i => new { i.SupplierId, i.Supplier!.Code, i.Supplier!.NameAr, i.CurrencyCode })
            .Select(g => new PurchasesBySupplierRowDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierCode = g.Key.Code,
                SupplierNameAr = g.Key.NameAr,
                CurrencyCode = g.Key.CurrencyCode,
                InvoiceCount = g.Count(),
                TotalAmount = g.Sum(i => i.TotalAmount),
                TotalPaid = g.Sum(i => i.AmountPaid)
            })
            .OrderBy(r => r.SupplierCode).ThenBy(r => r.CurrencyCode)
            .ToListAsync(cancellationToken);
}

// ---- Report #2: مشتريات حسب الصنف (Purchases by Item) — تفاعلي ----

public sealed class PurchasesByItemRowDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal TotalQuantity { get; init; }
    public required decimal TotalValue { get; init; }
    public required decimal AverageUnitPrice { get; init; }
}

public sealed record GetPurchasesByItemReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<PurchasesByItemRowDto>>;

public sealed class GetPurchasesByItemReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchasesByItemReportQuery, IReadOnlyList<PurchasesByItemRowDto>>
{
    private static readonly PurchaseInvoiceStatus[] PostedStatuses =
    [
        PurchaseInvoiceStatus.Posted, PurchaseInvoiceStatus.PartiallyPaid, PurchaseInvoiceStatus.Paid, PurchaseInvoiceStatus.Overdue
    ];

    public async Task<IReadOnlyList<PurchasesByItemRowDto>> Handle(GetPurchasesByItemReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseInvoiceLines
            .AsNoTracking()
            .Where(l => PostedStatuses.Contains(l.PurchaseInvoice!.Status)
                && l.PurchaseInvoice!.InvoiceDate >= request.From && l.PurchaseInvoice!.InvoiceDate <= request.To)
            .GroupBy(l => new { l.ItemId, l.Item!.Code, l.Item!.NameAr })
            .Select(g => new PurchasesByItemRowDto
            {
                ItemId = g.Key.ItemId,
                ItemCode = g.Key.Code,
                ItemNameAr = g.Key.NameAr,
                TotalQuantity = g.Sum(l => l.Quantity),
                TotalValue = g.Sum(l => l.TotalPrice),
                AverageUnitPrice = g.Average(l => l.UnitPrice)
            })
            .OrderByDescending(r => r.TotalValue)
            .ToListAsync(cancellationToken);
}

// ---- Report #10: تحليل توزيع المصروفات الإضافية (Additional Cost Allocation Analysis) — تفاعلي ----

public sealed class AllocationMethodSummaryDto
{
    public required string AllocationMethod { get; init; }
    public required int InvoiceCount { get; init; }
    public required decimal TotalAdditionalCosts { get; init; }
}

public sealed class AllocationByItemRowDto
{
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal TotalAllocatedAdditionalCost { get; init; }
}

public sealed class AdditionalCostAllocationReportDto
{
    public required IReadOnlyList<AllocationMethodSummaryDto> ByMethod { get; init; }
    public required IReadOnlyList<AllocationByItemRowDto> ByItem { get; init; }
}

public sealed record GetAdditionalCostAllocationReportQuery(DateOnly From, DateOnly To) : IRequest<AdditionalCostAllocationReportDto>;

public sealed class GetAdditionalCostAllocationReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAdditionalCostAllocationReportQuery, AdditionalCostAllocationReportDto>
{
    public async Task<AdditionalCostAllocationReportDto> Handle(GetAdditionalCostAllocationReportQuery request, CancellationToken cancellationToken)
    {
        var invoicesInRange = db.PurchaseInvoices
            .AsNoTracking()
            .Where(i => i.AdditionalCosts > 0 && i.InvoiceDate >= request.From && i.InvoiceDate <= request.To);

        var byMethod = await invoicesInRange
            .GroupBy(i => i.AdditionalCostAllocationMethod)
            .Select(g => new AllocationMethodSummaryDto
            {
                AllocationMethod = g.Key == null ? "None" : g.Key.ToString()!,
                InvoiceCount = g.Count(),
                TotalAdditionalCosts = g.Sum(i => i.AdditionalCosts)
            })
            .ToListAsync(cancellationToken);

        var byItem = await db.PurchaseInvoiceLines
            .AsNoTracking()
            .Where(l => l.AllocatedAdditionalCost > 0
                && l.PurchaseInvoice!.InvoiceDate >= request.From && l.PurchaseInvoice!.InvoiceDate <= request.To)
            .GroupBy(l => new { l.ItemId, l.Item!.Code, l.Item!.NameAr })
            .Select(g => new AllocationByItemRowDto
            {
                ItemId = g.Key.ItemId,
                ItemCode = g.Key.Code,
                ItemNameAr = g.Key.NameAr,
                TotalAllocatedAdditionalCost = g.Sum(l => l.AllocatedAdditionalCost)
            })
            .OrderByDescending(r => r.TotalAllocatedAdditionalCost)
            .ToListAsync(cancellationToken);

        return new AdditionalCostAllocationReportDto { ByMethod = byMethod, ByItem = byItem };
    }
}
