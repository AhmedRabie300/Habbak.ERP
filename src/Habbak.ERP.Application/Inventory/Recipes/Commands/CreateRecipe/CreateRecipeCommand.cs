using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.Recipes.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.CreateRecipe;

/// <summary>Creates the first version (VersionNumber = 1) of a new recipe family, as Draft (screen
/// #16). A later version of the same family is created only through CreateNewRecipeVersionCommand,
/// never through this command — RecipeFamilyCode is generated here exactly once per family.</summary>
public sealed record CreateRecipeCommand : IRequest<long>
{
    public required long OutputItemId { get; init; }
    public required decimal OutputQuantity { get; init; }
    public required decimal WastePercentage { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public required IReadOnlyList<RecipeLineInput> Lines { get; init; }
}

public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.OutputItemId).GreaterThan(0);
        RuleFor(x => x.OutputQuantity).GreaterThan(0);
        RuleFor(x => x.WastePercentage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EffectiveFromDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("الوصفة تحتاج مكوّن واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ComponentItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class CreateRecipeCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateRecipeCommand, long>
{
    public async Task<long> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        var outputItem = await db.Items.FirstOrDefaultAsync(i => i.Id == request.OutputItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), request.OutputItemId);

        // Rule 27: a raw material is purchased only, never produced by a recipe.
        if (outputItem.ItemType is not (ItemType.SemiFinished or ItemType.FinishedGood))
        {
            throw new BusinessRuleException("INV-R27-INVALID-OUTPUT-ITEM-TYPE", "الصنف الناتج للوصفة لازم يكون نصف مصنَّع أو تام الصنع.");
        }

        await RecipeCycleChecker.EnsureNoCycleAsync(
            db, request.OutputItemId, request.Lines.Select(l => l.ComponentItemId), excludeRecipeId: null, cancellationToken);

        var recipeFamilyCode = await codeGenerator.ResolveCodeAsync("INVENTORY_RECIPE", null, cancellationToken);

        var recipe = new Recipe
        {
            CompanyId = currentCompanyContext.CompanyId,
            RecipeFamilyCode = recipeFamilyCode,
            VersionNumber = 1,
            IsCurrentVersion = true,
            OutputItemId = request.OutputItemId,
            OutputQuantity = request.OutputQuantity,
            WastePercentage = request.WastePercentage,
            Status = RecipeStatus.Draft,
            EffectiveFromDate = request.EffectiveFromDate
        };

        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ComponentItemId), cancellationToken);
        foreach (var line in request.Lines)
        {
            var unit = units.Resolve(line.ComponentItemId, line.UnitId);
            recipe.Lines.Add(new RecipeLine { ComponentItemId = line.ComponentItemId, Quantity = line.Quantity, UnitId = unit.UnitId, UnitFactor = unit.Factor });
        }

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);

        return recipe.Id;
    }
}
