using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SendRFQ;

/// <summary>Draft → Sent (section 6.2) — marks the RFQ as transmitted to its invited suppliers. No
/// actual email/fax integration, same as PurchaseOrder's own Send.</summary>
public sealed record SendRFQCommand(long Id) : IRequest;

public sealed class SendRFQCommandHandler(IApplicationDbContext db) : IRequestHandler<SendRFQCommand>
{
    public async Task Handle(SendRFQCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.Id);

        if (rfq.Status != RFQStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-DRAFT", "لا يمكن إرسال طلب عروض الأسعار إلا وهو في حالة مسودة.");
        }

        rfq.Status = RFQStatus.Sent;

        await db.SaveChangesAsync(cancellationToken);
    }
}
