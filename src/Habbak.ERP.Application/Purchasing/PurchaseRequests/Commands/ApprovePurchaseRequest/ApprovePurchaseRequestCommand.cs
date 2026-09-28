using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.ApprovePurchaseRequest;

public sealed record ApprovePurchaseRequestCommand(long Id) : IRequest;

public sealed class ApprovePurchaseRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<ApprovePurchaseRequestCommand>
{
    public async Task Handle(ApprovePurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        if (purchaseRequest.Status != PurchaseRequestStatus.PendingApproval)
        {
            throw new BusinessRuleException("PUR-REQUEST-NOT-PENDING", "لا يمكن اعتماد طلب الشراء إلا وهو بانتظار الاعتماد.");
        }

        purchaseRequest.Status = PurchaseRequestStatus.Approved;

        await db.SaveChangesAsync(cancellationToken);
    }
}
