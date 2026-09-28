using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.SubmitRecipe;

/// <summary>Rule 14: any new or edited recipe must go through PendingApproval before it can be used
/// in any production order or real-time sale — no exceptions.</summary>
public sealed record SubmitRecipeCommand(long Id) : IRequest;

public sealed class SubmitRecipeCommandHandler(IApplicationDbContext db) : IRequestHandler<SubmitRecipeCommand>
{
    public async Task Handle(SubmitRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.Id);

        if (recipe.Status != RecipeStatus.Draft)
        {
            throw new BusinessRuleException("INV-RECIPE-NOT-DRAFT", "لا يمكن إرسال الوصفة للاعتماد إلا وهي في حالة مسودة.");
        }

        recipe.Status = RecipeStatus.PendingApproval;

        await db.SaveChangesAsync(cancellationToken);
    }
}
