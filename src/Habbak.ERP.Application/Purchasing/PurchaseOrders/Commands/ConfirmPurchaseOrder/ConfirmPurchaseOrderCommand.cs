using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.ConfirmPurchaseOrder;

/// <summary>Sent → Confirmed (section 6.3) — the supplier confirmed the order. Rule 14: once
/// Confirmed, the order can no longer be edited.</summary>
public sealed record ConfirmPurchaseOrderCommand(long Id) : IRequest;

public sealed class ConfirmPurchaseOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<ConfirmPurchaseOrderCommand>
{
    public async Task Handle(ConfirmPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        if (order.Status != PurchaseOrderStatus.Sent)
        {
            throw new BusinessRuleException("PUR-ORDER-NOT-SENT", "لا يمكن تأكيد أمر الشراء إلا وهو في حالة مُرسل.");
        }

        order.Status = PurchaseOrderStatus.Confirmed;

        await db.SaveChangesAsync(cancellationToken);
    }
}
