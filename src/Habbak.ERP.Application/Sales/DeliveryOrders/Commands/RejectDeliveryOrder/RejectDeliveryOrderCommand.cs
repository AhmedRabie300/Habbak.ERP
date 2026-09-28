using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Commands.RejectDeliveryOrder;

/// <summary>Draft → Rejected — terminal (قاعدة 22, section 4.4): a rejected delivery order never
/// posted any stock movement, so rejecting it has no inventory effect to undo.</summary>
public sealed record RejectDeliveryOrderCommand(long Id) : IRequest;

public sealed class RejectDeliveryOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectDeliveryOrderCommand>
{
    public async Task Handle(RejectDeliveryOrderCommand request, CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliveryOrder), request.Id);

        if (deliveryOrder.Status != DeliveryOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-DELIVERY-ORDER-NOT-DRAFT", "لا يمكن رفض أمر التسليم إلا وهو في حالة مسودة.");
        }

        deliveryOrder.Status = DeliveryOrderStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
