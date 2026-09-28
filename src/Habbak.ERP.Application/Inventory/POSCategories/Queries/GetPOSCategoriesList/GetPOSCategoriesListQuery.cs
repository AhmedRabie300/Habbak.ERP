using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.POSCategories.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.POSCategories.Queries.GetPOSCategoriesList;

/// <summary>Not a paginated GetList screen: a company's POS-category count is always small.</summary>
public sealed record GetPOSCategoriesListQuery : IRequest<IReadOnlyList<POSCategoryDto>>;

public sealed class GetPOSCategoriesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPOSCategoriesListQuery, IReadOnlyList<POSCategoryDto>>
{
    public async Task<IReadOnlyList<POSCategoryDto>> Handle(GetPOSCategoriesListQuery request, CancellationToken cancellationToken)
    {
        return await db.POSCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new POSCategoryDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, DisplayOrder = c.DisplayOrder, IsActive = c.IsActive })
            .ToListAsync(cancellationToken);
    }
}
