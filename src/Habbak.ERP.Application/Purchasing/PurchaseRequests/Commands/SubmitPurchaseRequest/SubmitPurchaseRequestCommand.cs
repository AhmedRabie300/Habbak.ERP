using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.SubmitPurchaseRequest;

public sealed record SubmitPurchaseRequestCommand(long Id) : IRequest;

public sealed class SubmitPurchaseRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitPurchaseRequestCommand>
{
    public async Task Handle(SubmitPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        if (purchaseRequest.Status != PurchaseRequestStatus.Draft)
        {
            throw new BusinessRuleException("PUR-REQUEST-NOT-DRAFT", "لا يمكن إرسال طلب الشراء للاعتماد إلا وهو في حالة مسودة.");
        }

        purchaseRequest.Status = PurchaseRequestStatus.PendingApproval;

        await db.SaveChangesAsync(cancellationToken);
    }
}
