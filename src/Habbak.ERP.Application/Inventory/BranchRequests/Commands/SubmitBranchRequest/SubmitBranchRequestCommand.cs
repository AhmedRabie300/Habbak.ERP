using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Commands.SubmitBranchRequest;

/// <summary>
/// Moves a Draft request to PendingApproval (section 4.2). Rule 4 (RequestedQuantity vs
/// BranchItemLimit) is checked here — submission, not Draft-save, is this entity's equivalent of
/// a "posting-time" validation boundary (mirrors how WarehouseDocument enforces its own rules only
/// at Post, not at Create/Update).
/// </summary>
public sealed record SubmitBranchRequestCommand(long Id) : IRequest;

public sealed class SubmitBranchRequestCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitBranchRequestCommand>
{
    public async Task Handle(SubmitBranchRequestCommand request, CancellationToken cancellationToken)
    {
        var branchRequest = await db.BranchRequests
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BranchRequest), request.Id);

        if (branchRequest.Status != BranchRequestStatus.Draft)
        {
            throw new BusinessRuleException("INV-BRANCH-REQUEST-NOT-DRAFT", "لا يمكن تقديم الطلب إلا وهو في حالة مسودة.");
        }

        var itemIds = branchRequest.Lines.Select(l => l.ItemId).ToList();
        var limitsByItem = await db.BranchItemLimits
            .Where(l => l.BranchId == branchRequest.BranchId && itemIds.Contains(l.ItemId))
            .ToDictionaryAsync(l => l.ItemId, cancellationToken);

        foreach (var line in branchRequest.Lines)
        {
            if (!limitsByItem.TryGetValue(line.ItemId, out var limit))
            {
                continue;
            }

            // BranchItemLimit is in the item's base unit.
            var requestedBase = ItemUnits.ToBase(line.RequestedQuantity, line.UnitFactor);
            if (requestedBase > limit.MaxRequestQuantity)
            {
                throw new BusinessRuleException(
                    "INV-R4-EXCEEDS-MAX-REQUEST",
                    $"الكمية المطلوبة للصنف رقم {line.ItemId} تتجاوز أقصى كمية مسموح بها ({limit.MaxRequestQuantity} بالوحدة الأساسية).");
            }

            if (limit.MinRequestQuantity is { } minQuantity && requestedBase < minQuantity)
            {
                throw new BusinessRuleException(
                    "INV-R4-BELOW-MIN-REQUEST",
                    $"الكمية المطلوبة للصنف رقم {line.ItemId} أقل من الحد الأدنى المسموح ({minQuantity} بالوحدة الأساسية).");
            }
        }

        branchRequest.Status = BranchRequestStatus.PendingApproval;

        await db.SaveChangesAsync(cancellationToken);
    }
}
