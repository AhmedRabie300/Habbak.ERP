using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.SubmitInventoryCountForSettlement;

/// <summary>Screen #14, stage 2→3 transition — InProgress → PendingSettlement, once every line has
/// a CountedQuantity recorded.</summary>
public sealed record SubmitInventoryCountForSettlementCommand(long Id) : IRequest;

public sealed class SubmitInventoryCountForSettlementCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SubmitInventoryCountForSettlementCommand>
{
    public async Task Handle(SubmitInventoryCountForSettlementCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.InProgress)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-INPROGRESS", "لا يمكن إرسال الجرد للتسوية إلا وهو قيد التنفيذ.");
        }

        if (count.Lines.Any(l => l.CountedQuantity is null))
        {
            throw new BusinessRuleException("INV-COUNT-NOT-FULLY-COUNTED", "لا يمكن إرسال الجرد للتسوية قبل إدخال الكمية المعدودة لكل الأصناف.");
        }

        count.Status = InventoryCountStatus.PendingSettlement;

        await db.SaveChangesAsync(cancellationToken);
    }
}
