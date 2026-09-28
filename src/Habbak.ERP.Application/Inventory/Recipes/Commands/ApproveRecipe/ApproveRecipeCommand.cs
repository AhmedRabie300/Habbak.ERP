using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.ApproveRecipe;

/// <summary>Screen #17 — approves a PendingApproval recipe version. Flips every sibling version of
/// the same RecipeFamilyCode to IsCurrentVersion = false first, then this one to true (rule 31),
/// and marks the output item as manufacturable — closing the loop Item.IsManufacturable's own doc
/// comment describes ("linked from the recipe definition screen after saving").</summary>
public sealed record ApproveRecipeCommand(long Id) : IRequest;

public sealed class ApproveRecipeCommandHandler(IApplicationDbContext db) : IRequestHandler<ApproveRecipeCommand>
{
    public async Task Handle(ApproveRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        if (recipe.Status != RecipeStatus.PendingApproval)
        {
            throw new BusinessRuleException("INV-RECIPE-NOT-PENDING", "لا يمكن اعتماد الوصفة إلا وهي بانتظار الاعتماد.");
        }

        var siblingVersions = await db.Recipes
            .Where(r => r.RecipeFamilyCode == recipe.RecipeFamilyCode && r.Id != recipe.Id && r.IsCurrentVersion)
            .ToListAsync(cancellationToken);

        foreach (var sibling in siblingVersions)
        {
            sibling.IsCurrentVersion = false;
        }

        recipe.Status = RecipeStatus.Approved;
        recipe.IsCurrentVersion = true;

        var outputItem = await db.Items.FirstOrDefaultAsync(i => i.Id == recipe.OutputItemId, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), recipe.OutputItemId);
        outputItem.IsManufacturable = true;

        await db.SaveChangesAsync(cancellationToken);
    }
}
