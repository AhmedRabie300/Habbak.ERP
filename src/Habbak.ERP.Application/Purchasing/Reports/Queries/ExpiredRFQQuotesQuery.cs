using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Reports.Queries;

// ---- العروض منتهية الصلاحية (Expired RFQ Quotes) — rule 18's own flip side: surfaces quotes
// stuck in an open RFQ that can no longer ever be selected (ValidUntil is in the past). ----

public sealed class ExpiredRFQQuoteRowDto
{
    public required long QuoteId { get; init; }
    public required long RFQId { get; init; }
    public required string RFQNumber { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required string SupplierCode { get; init; }
    public required string SupplierNameAr { get; init; }
    public required decimal UnitPrice { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public required int DaysExpired { get; init; }
}

/// <summary>Only RFQs still open (Sent/UnderReview) matter here — once Awarded/Cancelled/Archived
/// an expired, unselected quote is moot. From/To further narrows by ValidUntil, on top of the
/// unconditional "already expired" (&lt; today) rule.</summary>
public sealed record GetExpiredRFQQuotesReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<ExpiredRFQQuoteRowDto>>;

public sealed class GetExpiredRFQQuotesReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetExpiredRFQQuotesReportQuery, IReadOnlyList<ExpiredRFQQuoteRowDto>>
{
    private static readonly RFQStatus[] OpenStatuses = [RFQStatus.Sent, RFQStatus.UnderReview];

    public async Task<IReadOnlyList<ExpiredRFQQuoteRowDto>> Handle(GetExpiredRFQQuotesReportQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await db.RFQSupplierQuotes
            .AsNoTracking()
            .Include(q => q.RFQLine).ThenInclude(l => l!.Item)
            .Include(q => q.RFQLine).ThenInclude(l => l!.RFQ)
            .Include(q => q.RFQSupplier).ThenInclude(s => s!.Supplier)
            .Where(q => !q.IsSelected && q.ValidUntil < today && q.ValidUntil >= request.From && q.ValidUntil <= request.To
                && OpenStatuses.Contains(q.RFQLine!.RFQ!.Status))
            .Select(q => new
            {
                QuoteId = q.Id,
                RFQId = q.RFQLine!.RFQId,
                RFQNumber = q.RFQLine!.RFQ!.RFQNumber,
                ItemCode = q.RFQLine!.Item!.Code,
                ItemNameAr = q.RFQLine!.Item!.NameAr,
                SupplierCode = q.RFQSupplier!.Supplier!.Code,
                SupplierNameAr = q.RFQSupplier!.Supplier!.NameAr,
                q.UnitPrice,
                q.ValidUntil
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ExpiredRFQQuoteRowDto
            {
                QuoteId = r.QuoteId,
                RFQId = r.RFQId,
                RFQNumber = r.RFQNumber,
                ItemCode = r.ItemCode,
                ItemNameAr = r.ItemNameAr,
                SupplierCode = r.SupplierCode,
                SupplierNameAr = r.SupplierNameAr,
                UnitPrice = r.UnitPrice,
                ValidUntil = r.ValidUntil,
                DaysExpired = today.DayNumber - r.ValidUntil.DayNumber
            })
            .OrderByDescending(r => r.DaysExpired)
            .ToList();
    }
}
