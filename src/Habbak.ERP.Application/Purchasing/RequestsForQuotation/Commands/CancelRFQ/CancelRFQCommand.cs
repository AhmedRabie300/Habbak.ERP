using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.CancelRFQ;

/// <summary>Draft/Sent/UnderReview → Cancelled — rule 19 style, no cancelling after Awarded.</summary>
public sealed record CancelRFQCommand(long Id) : IRequest;

public sealed class CancelRFQCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelRFQCommand>
{
    public async Task Handle(CancelRFQCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.Id);

        if (rfq.Status is not (RFQStatus.Draft or RFQStatus.Sent or RFQStatus.UnderReview))
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-CANCELLABLE", "لا يمكن إلغاء طلب عروض الأسعار في حالته الحالية.");
        }

        rfq.Status = RFQStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
