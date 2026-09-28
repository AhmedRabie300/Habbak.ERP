using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.Recipes.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Recipes.Queries.GetRecipesList;

/// <summary>Screen #16's List — scoped to current versions only (IsCurrentVersion = true), so
/// superseded historical versions (kept only for old ProductionOrder references, rule 31) don't
/// clutter the everyday list; version history isn't a separate screen in this pass.</summary>
public sealed class GetRecipesListQuery : ListQuery, IRequest<PagedResult<RecipeListItemDto>>;

public sealed class GetRecipesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRecipesListQuery, PagedResult<RecipeListItemDto>>
{
    public async Task<PagedResult<RecipeListItemDto>> Handle(GetRecipesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Recipes.AsNoTracking().Include(r => r.OutputItem).Where(r => r.IsCurrentVersion);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<RecipeStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(r =>
                r.RecipeFamilyCode.Contains(term) ||
                r.OutputItem!.Code.Contains(term) ||
                r.OutputItem!.NameAr.Contains(term) ||
                matchingStatuses.Contains(r.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => descending ? query.OrderByDescending(r => r.RecipeFamilyCode) : query.OrderBy(r => r.RecipeFamilyCode)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RecipeListItemDto
            {
                Id = r.Id,
                RecipeFamilyCode = r.RecipeFamilyCode,
                VersionNumber = r.VersionNumber,
                OutputItemId = r.OutputItemId,
                OutputItemCode = r.OutputItem!.Code,
                OutputItemNameAr = r.OutputItem!.NameAr,
                OutputQuantity = r.OutputQuantity,
                IsCurrentVersion = r.IsCurrentVersion,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
