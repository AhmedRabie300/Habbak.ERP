using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.CancelPurchaseReturn;

/// <summary>Draft → Cancelled — rule 19: no cancelling after posting (stock already moved).</summary>
public sealed record CancelPurchaseReturnCommand(long Id) : IRequest;

public sealed class CancelPurchaseReturnCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelPurchaseReturnCommand>
{
    public async Task Handle(CancelPurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await db.PurchaseReturns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseReturn), request.Id);

        if (purchaseReturn.Status != PurchaseReturnStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RETURN-NOT-CANCELLABLE", "لا يمكن إلغاء مردود المشتريات إلا وهو في حالة مسودة.");
        }

        purchaseReturn.Status = PurchaseReturnStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
