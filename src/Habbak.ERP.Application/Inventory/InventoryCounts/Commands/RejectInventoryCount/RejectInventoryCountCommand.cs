using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.RejectInventoryCount;

/// <summary>Rule 20: Rejected is terminal — branches off Settled instead of Closing (section 4.3);
/// an approval chain over the settlement can reject the whole count rather than letting it post.</summary>
public sealed record RejectInventoryCountCommand(long Id) : IRequest;

public sealed class RejectInventoryCountCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectInventoryCountCommand>
{
    public async Task Handle(RejectInventoryCountCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.Settled)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-SETTLED", "لا يمكن رفض الجرد إلا وهو في حالة تمت التسوية.");
        }

        count.Status = InventoryCountStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
