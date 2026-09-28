using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Commands.CancelSalesOrder;

/// <summary>Draft/Confirmed → Cancelled — safe any time before delivery/invoicing actually starts
/// (unreachable yet in this phase).</summary>
public sealed record CancelSalesOrderCommand(long Id) : IRequest;

public sealed class CancelSalesOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelSalesOrderCommand>
{
    public async Task Handle(CancelSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.Id);

        if (order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Confirmed))
        {
            throw new BusinessRuleException("SALES-ORDER-NOT-CANCELLABLE", "لا يمكن إلغاء أمر البيع بعد بدء التسليم أو الفوترة أو رفضه أو إلغاءه بالفعل.");
        }

        order.Status = SalesOrderStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
