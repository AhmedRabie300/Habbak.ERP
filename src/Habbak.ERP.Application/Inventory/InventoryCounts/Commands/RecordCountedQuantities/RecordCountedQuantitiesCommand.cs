using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.RecordCountedQuantities;

/// <summary>Screen #14, stage 2 ("Counters" — barcode-reader integration enters these progressively,
/// not necessarily all at once) — InProgress-only. Recomputes VarianceQuantity the moment each
/// CountedQuantity is entered, rather than deferring the computation to settlement time.</summary>
public sealed record RecordCountedQuantitiesCommand : IRequest
{
    public required long Id { get; init; }
    public required IReadOnlyList<CountedQuantityInput> Lines { get; init; }
}

public sealed class RecordCountedQuantitiesCommandValidator : AbstractValidator<RecordCountedQuantitiesCommand>
{
    public RecordCountedQuantitiesCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineId).GreaterThan(0);
            line.RuleFor(l => l.CountedQuantity).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class RecordCountedQuantitiesCommandHandler(IApplicationDbContext db) : IRequestHandler<RecordCountedQuantitiesCommand>
{
    public async Task Handle(RecordCountedQuantitiesCommand request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        if (count.Status != InventoryCountStatus.InProgress)
        {
            throw new BusinessRuleException("INV-COUNT-NOT-INPROGRESS", "لا يمكن إدخال الكميات المعدودة إلا والجرد قيد التنفيذ.");
        }

        var linesById = count.Lines.ToDictionary(l => l.Id);
        var units = await ItemUnits.LoadAsync(db, count.Lines.Select(l => l.ItemId), cancellationToken);

        foreach (var input in request.Lines)
        {
            if (!linesById.TryGetValue(input.LineId, out var line))
            {
                throw new NotFoundException(nameof(InventoryCountLine), input.LineId);
            }

            if (input.UnitId is { } unitId && unitId != line.UnitId)
            {
                var unit = units.Resolve(line.ItemId, unitId);
                line.UnitId = unit.UnitId;
                line.UnitFactor = unit.Factor;
            }

            // Counted in the line's unit; the system quantity and the variance are in base units.
            line.CountedQuantity = input.CountedQuantity;
            line.VarianceQuantity = ItemUnits.ToBase(input.CountedQuantity, line.UnitFactor) - line.SystemQuantity;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
