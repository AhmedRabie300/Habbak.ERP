using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CreateInventoryCount;

/// <summary>
/// Screen #14, stage 1 ("Create"). Rule 9: Full auto-populates a line for every active item
/// already stocked in this warehouse (scope decision: "active in this warehouse" is read as
/// having an existing StockBalance row there — Item itself carries no per-warehouse master list,
/// only StockBalance/ItemWarehouseSettings do, and a StockBalance row is the more literal match for
/// "stocked in the warehouse" than a settings row that could exist with zero actual movement).
/// Partial/Cyclic instead take the caller's own ItemIds (rule 9's "manual selection only").
///
/// Rule 12: a warehouse can't have two counts open at once — checked here at creation, the
/// earliest point a count "opens".
/// </summary>
public sealed record CreateInventoryCountCommand : IRequest<long>
{
    public required long WarehouseId { get; init; }
    public required DateOnly CountDate { get; init; }
    public required InventoryCountType CountType { get; init; }

    /// <summary>Required for Partial/Cyclic, ignored for Full (which auto-populates instead).</summary>
    public IReadOnlyList<long>? ItemIds { get; init; }
}

public sealed class CreateInventoryCountCommandValidator : AbstractValidator<CreateInventoryCountCommand>
{
    public CreateInventoryCountCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.CountDate).NotEqual(default(DateOnly));
        RuleFor(x => x.ItemIds)
            .Must(ids => ids is { Count: > 0 })
            .When(x => x.CountType is InventoryCountType.Partial or InventoryCountType.Cyclic)
            .WithMessage("الجرد الجزئي/الدائري يتطلب اختيار صنف واحد على الأقل.");
    }
}

public sealed class CreateInventoryCountCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateInventoryCountCommand, long>
{
    public async Task<long> Handle(CreateInventoryCountCommand request, CancellationToken cancellationToken)
    {
        var hasOpenCount = await db.InventoryCounts.AnyAsync(
            c => c.WarehouseId == request.WarehouseId
                && (c.Status == InventoryCountStatus.InProgress || c.Status == InventoryCountStatus.PendingSettlement),
            cancellationToken);
        if (hasOpenCount)
        {
            throw new BusinessRuleException("INV-R12-COUNT-ALREADY-OPEN", "يوجد جرد آخر قيد التنفيذ أو بانتظار التسوية على هذا المخزن بالفعل.");
        }

        var balances = await db.StockBalances
            .Where(b => b.WarehouseId == request.WarehouseId)
            .ToDictionaryAsync(b => b.ItemId, cancellationToken);

        IReadOnlyList<long> itemIds;
        if (request.CountType == InventoryCountType.Full)
        {
            itemIds = await db.Items
                .Where(i => i.IsActive && balances.Keys.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            itemIds = request.ItemIds!;
        }

        var countNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_COUNT", null, cancellationToken);

        var inventoryCount = new InventoryCount
        {
            CompanyId = currentCompanyContext.CompanyId,
            WarehouseId = request.WarehouseId,
            CountNumber = countNumber,
            CountDate = request.CountDate,
            CountType = request.CountType,
            Status = InventoryCountStatus.Draft
        };

        // A count starts in each item's base unit; the counter may switch a line to another unit.
        var units = await ItemUnits.LoadAsync(db, itemIds, cancellationToken);
        foreach (var itemId in itemIds)
        {
            balances.TryGetValue(itemId, out var balance);
            inventoryCount.Lines.Add(new InventoryCountLine
            {
                ItemId = itemId,
                SystemQuantity = balance?.QuantityOnHand ?? 0,
                UnitId = units.Base(itemId).UnitId,
                UnitFactor = 1
            });
        }

        db.InventoryCounts.Add(inventoryCount);
        await db.SaveChangesAsync(cancellationToken);

        return inventoryCount.Id;
    }
}
