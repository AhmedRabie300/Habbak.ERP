using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.SettleInventoryCountLine;

/// <summary>Screen #14, stage 3 (interactive settlement) — rule 10: each line's decision is
/// independent, made one at a time; accepting one variance implies nothing about the rest.</summary>
public sealed record SettleInventoryCountLineCommand : IRequest
{
    public required long Id { get; init; }
    public required long LineId { get; init; }
    public required SettlementDecision Decision { get; init; }
    public string? SettlementReason { get; init; }
}

public sealed class SettleInventoryCountLineCommandValidator : AbstractValidator<SettleInventoryCountLineCommand>
{
    public SettleInventoryCountLineCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.LineId).GreaterThan(0);
        RuleFor(x => x.Decision).NotEqual(SettlementDecision.Pending).WithMessage("يجب اختيار قبول أو رفض الفرق.");
    }
}

public sealed class SettleInventoryCountLineCommandHandler(IApplicationDbContext db) : IRequestHandler<SettleInventoryCountLineCommand>
{
    public async Task Handle(SettleInventoryCountLineCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.PendingSettlement)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-PENDING-SETTLEMENT", "لا يمكن تسوية سطر إلا والجرد بانتظار التسوية.");
        }

        var line = count.Lines.FirstOrDefault(l => l.Id == request.LineId)
            ?? throw new NotFoundException(nameof(InventoryCountLine), request.LineId);

        if (line.VarianceQuantity is not (null or 0) && string.IsNullOrWhiteSpace(request.SettlementReason))
        {
            throw new BusinessRuleException("INV-COUNT-SETTLEMENT-REASON-REQUIRED", "سبب التسوية إلزامي لوجود فرق في هذا الصنف.");
        }

        line.SettlementDecision = request.Decision;
        line.SettlementReason = request.SettlementReason;

        await db.SaveChangesAsync(cancellationToken);
    }
}
