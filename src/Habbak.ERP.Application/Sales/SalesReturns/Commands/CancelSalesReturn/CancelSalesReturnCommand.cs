using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Commands.CancelSalesReturn;

/// <summary>Draft → Cancelled — no cancelling after posting (stock already moved), same rule 19
/// discipline as PurchaseReturn's own CancelPurchaseReturnCommand.</summary>
public sealed record CancelSalesReturnCommand(long Id) : IRequest;

public sealed class CancelSalesReturnCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelSalesReturnCommand>
{
    public async Task Handle(CancelSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var salesReturn = await db.SalesReturns.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesReturn), request.Id);

        if (salesReturn.Status != SalesReturnStatus.Draft)
        {
            throw new BusinessRuleException("SALES-RETURN-NOT-CANCELLABLE", "لا يمكن إلغاء مرتجع المبيعات إلا وهو في حالة مسودة.");
        }

        salesReturn.Status = SalesReturnStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
