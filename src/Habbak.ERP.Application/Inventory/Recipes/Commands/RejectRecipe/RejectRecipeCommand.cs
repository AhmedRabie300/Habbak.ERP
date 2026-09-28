using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.RejectRecipe;

/// <summary>Rule 20: Rejected is terminal — a rejected recipe is never edited and resubmitted; a
/// fresh attempt requires a brand-new recipe (or, for an existing Approved family, a new version
/// via CreateNewRecipeVersionCommand) created entirely from scratch.</summary>
public sealed record RejectRecipeCommand(long Id) : IRequest;

public sealed class RejectRecipeCommandHandler(IApplicationDbContext db) : IRequestHandler<RejectRecipeCommand>
{
    public async Task Handle(RejectRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        if (recipe.Status != RecipeStatus.PendingApproval)
        {
            throw new BusinessRuleException("INV-RECIPE-NOT-PENDING", "لا يمكن رفض الوصفة إلا وهي بانتظار الاعتماد.");
        }

        recipe.Status = RecipeStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);
    }
}
