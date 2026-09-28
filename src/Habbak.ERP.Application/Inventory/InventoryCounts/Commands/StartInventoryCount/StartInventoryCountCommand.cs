using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.StartInventoryCount;

/// <summary>Screen #14, stage 2's opening move — Draft → InProgress, unlocking CountedQuantity entry.</summary>
public sealed record StartInventoryCountCommand(long Id) : IRequest;

public sealed class StartInventoryCountCommandHandler(IApplicationDbContext db) : IRequestHandler<StartInventoryCountCommand>
{
    public async Task Handle(StartInventoryCountCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.Draft)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-DRAFT", "لا يمكن بدء الجرد إلا وهو في حالة مسودة.");
        }

        count.Status = InventoryCountStatus.InProgress;

        await db.SaveChangesAsync(cancellationToken);
    }
}
