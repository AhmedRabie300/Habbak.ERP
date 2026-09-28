using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Commands.RejectSalesOrder;

/// <summary>Draft → Rejected — terminal (قاعدة 22): a rejected order is never edited and resent, a
/// fresh order is required.</summary>
public sealed record RejectSalesOrderCommand(long Id) : IRequest;

public sealed class RejectSalesOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectSalesOrderCommand>
{
    public async Task Handle(RejectSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.Id);

        if (order.Status != SalesOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-ORDER-NOT-DRAFT", "لا يمكن رفض أمر البيع إلا وهو في حالة مسودة.");
        }

        order.Status = SalesOrderStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
