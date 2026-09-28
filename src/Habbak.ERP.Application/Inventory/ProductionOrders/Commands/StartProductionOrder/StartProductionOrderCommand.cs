using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Commands.StartProductionOrder;

public sealed record StartProductionOrderCommand(long Id) : IRequest;

public sealed class StartProductionOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<StartProductionOrderCommand>
{
    public async Task Handle(StartProductionOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionOrder), request.Id);

        if (order.Status != ProductionOrderStatus.Pending)
        {
            throw new BusinessRuleException("INV-PRODORDER-NOT-PENDING", "لا يمكن بدء تنفيذ أمر الإنتاج إلا وهو في حالة معلّق.");
        }

        order.Status = ProductionOrderStatus.InProgress;
        order.StartDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
    }
}
