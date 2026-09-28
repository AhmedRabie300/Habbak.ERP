using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Commands.UpdateProductionOrder;

/// <summary>Pending-only edit — once Started (InProgress) the plan is locked in; only completion
/// (ActualQuantity/EndDate) can still change the order after that.</summary>
public sealed record UpdateProductionOrderCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required long WarehouseId { get; init; }
    public required long RecipeId { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public DateOnly? StartDate { get; init; }
}

public sealed class UpdateProductionOrderCommandValidator : AbstractValidator<UpdateProductionOrderCommand>
{
    public UpdateProductionOrderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.RecipeId).GreaterThan(0);
        RuleFor(x => x.PlannedQuantity).GreaterThan(0);
    }
}

public sealed class UpdateProductionOrderCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductionOrderCommand>
{
    public async Task Handle(UpdateProductionOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionOrder), request.Id);

        if (order.Status != ProductionOrderStatus.Pending)
        {
            throw new BusinessRuleException("INV-PRODORDER-NOT-PENDING", "لا يمكن تعديل أمر الإنتاج إلا وهو في حالة معلّق.");
        }

        var recipe = await db.Recipes
            .Include(r => r.Lines).ThenInclude(l => l.ComponentItem)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.RecipeId);

        if (recipe.Status != RecipeStatus.Approved)
        {
            throw new BusinessRuleException("INV-PRODORDER-RECIPE-NOT-APPROVED", "لا يمكن إنشاء أمر إنتاج إلا من وصفة معتمدة.");
        }

        db.Entry(order).Property(nameof(ProductionOrder.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        var scalingFactor = request.PlannedQuantity / recipe.OutputQuantity;

        order.WarehouseId = request.WarehouseId;
        order.RecipeId = request.RecipeId;
        order.PlannedQuantity = request.PlannedQuantity;
        order.StartDate = request.StartDate;
        order.StandardCost = recipe.Lines.Sum(l => ItemUnits.ToBase(l.Quantity, l.UnitFactor) * scalingFactor * (l.ComponentItem!.StandardCost ?? 0));

        await db.SaveChangesAsync(cancellationToken);
    }
}
