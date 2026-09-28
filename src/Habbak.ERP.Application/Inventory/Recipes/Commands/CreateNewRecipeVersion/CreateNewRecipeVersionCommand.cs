using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Commands.CreateNewRecipeVersion;

/// <summary>Rule 31: editing an Approved recipe never touches its row — this clones a new Draft row
/// sharing the same RecipeFamilyCode with VersionNumber+1 and PreviousVersionId pointing back at the
/// source. The new version starts as an editable copy of the source's fields/lines and follows the
/// exact same Draft → PendingApproval → Approved path as a brand-new recipe (screen #16's "new
/// version" action, distinct from plain Create).</summary>
public sealed record CreateNewRecipeVersionCommand(long SourceRecipeId) : IRequest<long>;

public sealed class CreateNewRecipeVersionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateNewRecipeVersionCommand, long>
{
    public async Task<long> Handle(CreateNewRecipeVersionCommand request, CancellationToken cancellationToken)
    {
        var source = await db.Recipes
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.SourceRecipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), request.SourceRecipeId);

        if (source.Status != RecipeStatus.Approved)
        {
            throw new BusinessRuleException("INV-RECIPE-NOT-APPROVED", "لا يمكن إنشاء إصدار جديد إلا من وصفة معتمدة.");
        }

        var latestVersionNumber = await db.Recipes
            .Where(r => r.RecipeFamilyCode == source.RecipeFamilyCode)
            .MaxAsync(r => r.VersionNumber, cancellationToken);

        var newVersion = new Recipe
        {
            CompanyId = source.CompanyId,
            RecipeFamilyCode = source.RecipeFamilyCode,
            VersionNumber = latestVersionNumber + 1,
            PreviousVersionId = source.Id,
            IsCurrentVersion = false,
            OutputItemId = source.OutputItemId,
            OutputQuantity = source.OutputQuantity,
            WastePercentage = source.WastePercentage,
            Status = RecipeStatus.Draft,
            EffectiveFromDate = source.EffectiveFromDate
        };

        foreach (var line in source.Lines)
        {
            newVersion.Lines.Add(new RecipeLine { ComponentItemId = line.ComponentItemId, Quantity = line.Quantity, UnitId = line.UnitId, UnitFactor = line.UnitFactor });
        }

        db.Recipes.Add(newVersion);
        await db.SaveChangesAsync(cancellationToken);

        return newVersion.Id;
    }
}
