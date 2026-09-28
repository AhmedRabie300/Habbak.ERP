using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Commands.RejectSalesReturn;

/// <summary>Draft → Rejected — terminal (قاعدة 22): a rejected return never posted any stock
/// movement, so rejecting it has no inventory effect to undo.</summary>
public sealed record RejectSalesReturnCommand(long Id) : IRequest;

public sealed class RejectSalesReturnCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectSalesReturnCommand>
{
    public async Task Handle(RejectSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var salesReturn = await db.SalesReturns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesReturn), request.Id);

        if (salesReturn.Status != SalesReturnStatus.Draft)
        {
            throw new BusinessRuleException("SALES-RETURN-NOT-DRAFT", "لا يمكن رفض مرتجع المبيعات إلا وهو في حالة مسودة.");
        }

        salesReturn.Status = SalesReturnStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
