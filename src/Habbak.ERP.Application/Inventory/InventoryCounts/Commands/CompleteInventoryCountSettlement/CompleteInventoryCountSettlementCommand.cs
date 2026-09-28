using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CompleteInventoryCountSettlement;

/// <summary>Screen #14, stage 3→4 transition — PendingSettlement → Settled, once every line has a
/// non-Pending decision (rule's "كل السطور اتحسم قبولًا أو رفضًا").</summary>
public sealed record CompleteInventoryCountSettlementCommand(long Id) : IRequest;

public sealed class CompleteInventoryCountSettlementCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CompleteInventoryCountSettlementCommand>
{
    public async Task Handle(CompleteInventoryCountSettlementCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.PendingSettlement)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-PENDING-SETTLEMENT", "لا يمكن إتمام التسوية إلا والجرد بانتظار التسوية.");
        }

        if (count.Lines.Any(l => l.SettlementDecision == SettlementDecision.Pending))
        {
            throw new BusinessRuleException("INV-COUNT-LINES-NOT-SETTLED", "لا يمكن إتمام التسوية قبل حسم قرار كل الأصناف قبولًا أو رفضًا.");
        }

        count.Status = InventoryCountStatus.Settled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
