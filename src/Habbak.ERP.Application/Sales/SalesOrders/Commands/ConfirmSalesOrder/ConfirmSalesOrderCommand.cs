using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesOrders.Commands.ConfirmSalesOrder;

/// <summary>Draft → Confirmed (04-Module-Sales.md, section 4.2).</summary>
public sealed record ConfirmSalesOrderCommand(long Id) : IRequest;

public sealed class ConfirmSalesOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<ConfirmSalesOrderCommand>
{
    public async Task Handle(ConfirmSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), request.Id);

        if (order.Status != SalesOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-ORDER-NOT-DRAFT", "لا يمكن تأكيد أمر البيع إلا وهو في حالة مسودة.");
        }

        order.Status = SalesOrderStatus.Confirmed;

        await db.SaveChangesAsync(cancellationToken);
    }
}
