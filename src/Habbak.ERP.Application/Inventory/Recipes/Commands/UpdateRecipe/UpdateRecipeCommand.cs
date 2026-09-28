using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.Recipes.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.UpdateRecipe;

/// <summary>Edits a Draft recipe version — OutputItemId never changes after creation (fixed across
/// every version of the same family), only the quantities/components/date can be adjusted while
/// still Draft. Once Approved, this command no longer applies — CreateNewRecipeVersionCommand must
/// be used instead (rule 31).</summary>
public sealed record UpdateRecipeCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required decimal OutputQuantity { get; init; }
    public required decimal WastePercentage { get; init; }
    public required DateOnly EffectiveFromDate { get; init; }
    public required IReadOnlyList<RecipeLineInput> Lines { get; init; }
}

public sealed class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public sealed class UpdateRecipeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateRecipeCommand>
{
    public async Task Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        if (recipe.Status != RecipeStatus.Draft)
        {
            throw new BusinessRuleException("INV-RECIPE-NOT-DRAFT", "لا يمكن تعديل الوصفة إلا وهي في حالة مسودة.");
        }

        await RecipeCycleChecker.EnsureNoCycleAsync(
            db, recipe.OutputItemId, request.Lines.Select(l => l.ComponentItemId), excludeRecipeId: recipe.Id, cancellationToken);

        db.Entry(recipe).Property(nameof(Recipe.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        recipe.OutputQuantity = request.OutputQuantity;
        recipe.WastePercentage = request.WastePercentage;
        recipe.EffectiveFromDate = request.EffectiveFromDate;

        // Units are checked before the old lines go, so a refused unit leaves the recipe as it was.
        var units = await ItemUnits.LoadAsync(db, request.Lines.Select(l => l.ComponentItemId), cancellationToken);
        var lineUnits = request.Lines.Select(l => units.Resolve(l.ComponentItemId, l.UnitId)).ToList();

        db.RecipeLines.RemoveRange(recipe.Lines);
        recipe.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (line, unit) in request.Lines.Zip(lineUnits))
        {
            recipe.Lines.Add(new RecipeLine { ComponentItemId = line.ComponentItemId, Quantity = line.Quantity, UnitId = unit.UnitId, UnitFactor = unit.Factor });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
