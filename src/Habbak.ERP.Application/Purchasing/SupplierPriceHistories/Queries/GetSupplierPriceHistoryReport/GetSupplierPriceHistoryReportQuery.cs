using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierPriceHistories.Queries.GetSupplierPriceHistoryReport;

public sealed class SupplierPriceHistoryRowDto
{
    public required long Id { get; init; }
    public required long SupplierId { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal UnitPrice { get; init; }
    public required string UnitCode { get; init; }
    public required DateOnly EffectiveDate { get; init; }
    public string? PurchaseInvoiceNumber { get; init; }

    /// <summary>UnitPrice minus the previous row's UnitPrice for the same Supplier+Item — null on
    /// the very first recorded price. Positive = price went up since last time.</summary>
    public decimal? PriceChange { get; init; }
}

/// <summary>Report screen #13 (03-Module-Purchasing.md, section 4.9/11) — read-only, no List/Edit.
/// Filterable by supplier and/or item; always ordered oldest-to-newest per Supplier+Item so
/// PriceChange can be computed as a running diff.</summary>
public sealed record GetSupplierPriceHistoryReportQuery(long? SupplierId, long? ItemId)
    : IRequest<IReadOnlyList<SupplierPriceHistoryRowDto>>;

public sealed class GetSupplierPriceHistoryReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierPriceHistoryReportQuery, IReadOnlyList<SupplierPriceHistoryRowDto>>
{
    public async Task<IReadOnlyList<SupplierPriceHistoryRowDto>> Handle(
        GetSupplierPriceHistoryReportQuery request, CancellationToken cancellationToken)
    {
        var query = db.SupplierPriceHistories
            .AsNoTracking()
            .Include(h => h.Supplier)
            .Include(h => h.Item)
            .Include(h => h.Unit)
            .Include(h => h.PurchaseInvoice)
            .AsQueryable();

        if (request.SupplierId is { } supplierId)
        {
            query = query.Where(h => h.SupplierId == supplierId);
        }

        if (request.ItemId is { } itemId)
        {
            query = query.Where(h => h.ItemId == itemId);
        }

        var rows = await query
            .OrderBy(h => h.SupplierId).ThenBy(h => h.ItemId).ThenBy(h => h.EffectiveDate).ThenBy(h => h.Id)
            .Select(h => new
            {
                h.Id,
                h.SupplierId,
                SupplierCode = h.Supplier!.Code,
                SupplierNameAr = h.Supplier!.NameAr,
                h.ItemId,
                ItemCode = h.Item!.Code,
                ItemNameAr = h.Item!.NameAr,
                h.UnitPrice,
                UnitCode = h.Unit!.Code,
                h.EffectiveDate,
                PurchaseInvoiceNumber = h.PurchaseInvoice != null ? h.PurchaseInvoice.InvoiceNumber : null
            })
            .ToListAsync(cancellationToken);

        var result = new List<SupplierPriceHistoryRowDto>(rows.Count);
        decimal? previousPrice = null;
        (long SupplierId, long ItemId)? previousKey = null;

        foreach (var row in rows)
        {
            var key = (row.SupplierId, row.ItemId);
            var priceChange = previousKey == key ? row.UnitPrice - previousPrice : null;

            result.Add(new SupplierPriceHistoryRowDto
            {
                Id = row.Id,
                SupplierId = row.SupplierId,
                SupplierCode = row.SupplierCode,
                SupplierNameAr = row.SupplierNameAr,
                ItemId = row.ItemId,
                ItemCode = row.ItemCode,
                ItemNameAr = row.ItemNameAr,
                UnitPrice = row.UnitPrice,
                UnitCode = row.UnitCode,
                EffectiveDate = row.EffectiveDate,
                PurchaseInvoiceNumber = row.PurchaseInvoiceNumber,
                PriceChange = priceChange
            });

            previousPrice = row.UnitPrice;
            previousKey = key;
        }

        // Newest-first for display, the running diff above was computed oldest-first.
        result.Reverse();
        return result;
    }
}
