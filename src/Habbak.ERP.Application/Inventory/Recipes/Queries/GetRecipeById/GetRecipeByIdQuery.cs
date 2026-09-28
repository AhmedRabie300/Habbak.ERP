using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.Recipes.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Queries.GetRecipeById;

public sealed record GetRecipeByIdQuery(long Id) : IRequest<RecipeDetailDto>;

public sealed class GetRecipeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetRecipeByIdQuery, RecipeDetailDto>
{
    public async Task<RecipeDetailDto> Handle(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .AsNoTracking()
            .Include(r => r.OutputItem)
            .Include(r => r.Lines).ThenInclude(l => l.ComponentItem)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        var estimatedComponentCost = recipe.Lines.Sum(l => ItemUnits.ToBase(l.Quantity, l.UnitFactor) * (l.ComponentItem!.StandardCost ?? 0));

        return new RecipeDetailDto
        {
            Id = recipe.Id,
            RecipeFamilyCode = recipe.RecipeFamilyCode,
            VersionNumber = recipe.VersionNumber,
            PreviousVersionId = recipe.PreviousVersionId,
            IsCurrentVersion = recipe.IsCurrentVersion,
            OutputItemId = recipe.OutputItemId,
            OutputItemCode = recipe.OutputItem!.Code,
            OutputItemNameAr = recipe.OutputItem!.NameAr,
            OutputQuantity = recipe.OutputQuantity,
            WastePercentage = recipe.WastePercentage,
            Status = recipe.Status.ToString(),
            EffectiveFromDate = recipe.EffectiveFromDate,
            RowVersion = Convert.ToBase64String(recipe.RowVersion),
            EstimatedComponentCost = estimatedComponentCost,
            CostPerOutputUnit = recipe.OutputQuantity > 0 ? estimatedComponentCost / recipe.OutputQuantity : 0,
            Lines = recipe.Lines
                .Select(l => new RecipeLineDto
                {
                    Id = l.Id,
                    ComponentItemId = l.ComponentItemId,
                    ComponentItemCode = l.ComponentItem!.Code,
                    ComponentItemNameAr = l.ComponentItem!.NameAr,
                    Quantity = l.Quantity,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit?.Code,
                    UnitNameAr = l.Unit?.NameAr,
                    UnitFactor = l.UnitFactor,
                    ComponentStandardCost = l.ComponentItem!.StandardCost
                })
                .ToList()
        };
    }
}
