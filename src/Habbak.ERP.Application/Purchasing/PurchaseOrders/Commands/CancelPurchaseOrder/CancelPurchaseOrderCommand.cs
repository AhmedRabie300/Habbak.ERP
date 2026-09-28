using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;

/// <summary>Draft/Sent/Confirmed → Cancelled — safe any time before any goods have actually been
/// received against this order (ReceivedQuantity stays 0 until GoodsReceipt exists).</summary>
public sealed record CancelPurchaseOrderCommand(long Id) : IRequest;

public sealed class CancelPurchaseOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelPurchaseOrderCommand>
{
    public async Task Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent or PurchaseOrderStatus.Confirmed))
        {
            throw new BusinessRuleException("PUR-ORDER-NOT-CANCELLABLE", "لا يمكن إلغاء أمر الشراء في حالته الحالية.");
        }

        // Remarks7: a cancelled order converts nothing, so its quantities go back to the request.
        if (order.PurchaseRequestId is { } requestId)
        {
            var requestLines = await PurchaseOrderRequestLinking.LoadRequestLinesAsync(
                db, order.Lines.Where(l => l.PurchaseRequestLineId is not null).Select(l => l.PurchaseRequestLineId!.Value), cancellationToken);
            await PurchaseOrderRequestLinking.ApplyAsync(db, requestId, PurchaseOrderRequestLinking.PlanOf(order.Lines, requestLines), -1, cancellationToken);
        }

        order.Status = PurchaseOrderStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
