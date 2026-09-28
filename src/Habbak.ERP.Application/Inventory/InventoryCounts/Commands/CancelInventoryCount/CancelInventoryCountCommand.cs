using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CancelInventoryCount;

/// <summary>Not in the module doc's own state diagram, but every other cancellable entity in this
/// module (BranchRequest via Reject, ProductionOrder) offers an escape hatch before any stock
/// effect exists — Draft/InProgress is exactly that window here, since nothing posts until Close.</summary>
public sealed record CancelInventoryCountCommand(long Id) : IRequest;

public sealed class CancelInventoryCountCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelInventoryCountCommand>
{
    public async Task Handle(CancelInventoryCountCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status is not (InventoryCountStatus.Draft or InventoryCountStatus.InProgress))
        {
            throw new BusinessRuleException("INV-COUNT-NOT-CANCELLABLE", "لا يمكن إلغاء الجرد بعد إرساله للتسوية.");
        }

        count.Status = InventoryCountStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
