using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.AwardRFQ;

/// <summary>UnderReview → Awarded (section 6.2) — requires at least one selected quote; the user
/// then creates the PurchaseOrder by hand using the winning quote's numbers (no auto-copy wiring,
/// see the entity's own class doc).</summary>
public sealed record AwardRFQCommand(long Id) : IRequest;

public sealed class AwardRFQCommandHandler(IApplicationDbContext db) : IRequestHandler<AwardRFQCommand>
{
    public async Task Handle(AwardRFQCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.Id);

        if (rfq.Status != RFQStatus.UnderReview)
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-UNDER-REVIEW", "لا يمكن ترسية طلب عروض الأسعار إلا وهو قيد المراجعة.");
        }

        var hasSelectedQuote = await db.RFQSupplierQuotes
            .Join(db.RFQLines, q => q.RFQLineId, l => l.Id, (q, l) => new { q, l })
            .AnyAsync(x => x.l.RFQId == request.Id && x.q.IsSelected, cancellationToken);

        if (!hasSelectedQuote)
        {
            throw new BusinessRuleException("PUR-RFQ-NO-SELECTED-QUOTE", "لازم تختار عرض سعر واحد على الأقل قبل الترسية.");
        }

        rfq.Status = RFQStatus.Awarded;

        await db.SaveChangesAsync(cancellationToken);
    }
}
