using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CreateProductionOrder;

/// <summary>Screen #19 — Recipe must be Approved at creation time (module doc's field-table note);
/// StandardCost is estimated once here from the recipe's component costs scaled to PlannedQuantity
/// and never recomputed afterward, so it stays the fixed baseline ActualCost is compared against
/// at completion (rule 16).</summary>
public sealed record CreateProductionOrderCommand : IRequest<long>
{
    public required long WarehouseId { get; init; }
    public required long RecipeId { get; init; }
    public required decimal PlannedQuantity { get; init; }
    public DateOnly? StartDate { get; init; }
}

public sealed class CreateProductionOrderCommandValidator : AbstractValidator<CreateProductionOrderCommand>
{
    public CreateProductionOrderCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.RecipeId).GreaterThan(0);
        RuleFor(x => x.PlannedQuantity).GreaterThan(0);
    }
}

public sealed class CreateProductionOrderCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateProductionOrderCommand, long>
{
    public async Task<long> Handle(CreateProductionOrderCommand request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .Include(r => r.Lines).ThenInclude(l => l.ComponentItem)
            .FirstOrDefaultAsync(r => r.Id == request.RecipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.RecipeId);

        if (recipe.Status != RecipeStatus.Approved)
        {
            throw new BusinessRuleException("INV-PRODORDER-RECIPE-NOT-APPROVED", "لا يمكن إنشاء أمر إنتاج إلا من وصفة معتمدة.");
        }

        var scalingFactor = request.PlannedQuantity / recipe.OutputQuantity;
        var standardCost = recipe.Lines.Sum(l => ItemUnits.ToBase(l.Quantity, l.UnitFactor) * scalingFactor * (l.ComponentItem!.StandardCost ?? 0));

        var orderNumber = await codeGenerator.ResolveCodeAsync("INVENTORY_PRODUCTION_ORDER", null, cancellationToken);

        var order = new ProductionOrder
        {
            CompanyId = currentCompanyContext.CompanyId,
            WarehouseId = request.WarehouseId,
            RecipeId = request.RecipeId,
            OrderNumber = orderNumber,
            PlannedQuantity = request.PlannedQuantity,
            Status = ProductionOrderStatus.Pending,
            StartDate = request.StartDate,
            ExecutedByUserId = currentCompanyContext.UserId,
            StandardCost = standardCost
        };

        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
