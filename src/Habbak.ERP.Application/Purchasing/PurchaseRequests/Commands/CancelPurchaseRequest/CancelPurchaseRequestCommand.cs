using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.CancelPurchaseRequest;

/// <summary>Draft/PendingApproval/Approved → Cancelled — safe any time before the request is
/// actually Converted into a downstream document.</summary>
public sealed record CancelPurchaseRequestCommand(long Id) : IRequest;

public sealed class CancelPurchaseRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelPurchaseRequestCommand>
{
    public async Task Handle(CancelPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        if (purchaseRequest.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.PendingApproval or PurchaseRequestStatus.Approved))
        {
            throw new BusinessRuleException("PUR-REQUEST-NOT-CANCELLABLE", "لا يمكن إلغاء طلب الشراء بعد تحويله أو رفضه أو إلغاءه بالفعل.");
        }

        purchaseRequest.Status = PurchaseRequestStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
