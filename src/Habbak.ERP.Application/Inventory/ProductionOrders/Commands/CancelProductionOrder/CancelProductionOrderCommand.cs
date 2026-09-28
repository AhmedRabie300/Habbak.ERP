using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CancelProductionOrder;

/// <summary>Pending or InProgress → Cancelled — safe with no stock effect either way, since nothing
/// is posted to StockBalance/StockTransaction until CompleteProductionOrderCommand actually runs.</summary>
public sealed record CancelProductionOrderCommand(long Id) : IRequest;

public sealed class CancelProductionOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelProductionOrderCommand>
{
    public async Task Handle(CancelProductionOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionOrder), request.Id);

        if (order.Status is not (ProductionOrderStatus.Pending or ProductionOrderStatus.InProgress))
        {
            throw new BusinessRuleException("INV-PRODORDER-NOT-CANCELLABLE", "لا يمكن إلغاء أمر إنتاج مكتمل أو ملغى بالفعل.");
        }

        order.Status = ProductionOrderStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
