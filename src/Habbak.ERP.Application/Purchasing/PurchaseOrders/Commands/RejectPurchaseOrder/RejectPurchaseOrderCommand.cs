using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.RejectPurchaseOrder;

/// <summary>Draft/Sent → Rejected — terminal, same rule 20 pattern as every other approval-gated
/// entity: a rejected order is never edited and resent, a fresh attempt needs a new order.</summary>
public sealed record RejectPurchaseOrderCommand(long Id) : IRequest;

public sealed class RejectPurchaseOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectPurchaseOrderCommand>
{
    public async Task Handle(RejectPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent))
        {
            throw new BusinessRuleException("PUR-ORDER-NOT-REJECTABLE", "لا يمكن رفض أمر الشراء بعد تأكيده.");
        }

        // Remarks7: a rejected order converts nothing, so its quantities go back to the request.
        if (order.PurchaseRequestId is { } requestId)
        {
            var requestLines = await PurchaseOrderRequestLinking.LoadRequestLinesAsync(
                db, order.Lines.Where(l => l.PurchaseRequestLineId is not null).Select(l => l.PurchaseRequestLineId!.Value), cancellationToken);
            await PurchaseOrderRequestLinking.ApplyAsync(db, requestId, PurchaseOrderRequestLinking.PlanOf(order.Lines, requestLines), -1, cancellationToken);
        }

        order.Status = PurchaseOrderStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
