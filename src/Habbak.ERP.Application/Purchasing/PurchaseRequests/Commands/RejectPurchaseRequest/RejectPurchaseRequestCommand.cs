using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.RejectPurchaseRequest;

/// <summary>Rejected is terminal — a rejected request is never edited and resubmitted; a fresh
/// attempt needs a brand-new purchase request from scratch (same rule 20 pattern as every other
/// approval-gated entity in this codebase).</summary>
public sealed record RejectPurchaseRequestCommand(long Id) : IRequest;

public sealed class RejectPurchaseRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectPurchaseRequestCommand>
{
    public async Task Handle(RejectPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        if (purchaseRequest.Status != PurchaseRequestStatus.PendingApproval)
        {
            throw new BusinessRuleException("PUR-REQUEST-NOT-PENDING", "لا يمكن رفض طلب الشراء إلا وهو بانتظار الاعتماد.");
        }

        purchaseRequest.Status = PurchaseRequestStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
