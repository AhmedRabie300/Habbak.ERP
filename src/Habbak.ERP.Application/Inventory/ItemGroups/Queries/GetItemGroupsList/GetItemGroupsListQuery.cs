using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.ItemGroups.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ItemGroups.Queries.GetItemGroupsList;

/// <summary>Not a paginated GetList screen: a company's item-group count is always small.</summary>
public sealed record GetItemGroupsListQuery : IRequest<IReadOnlyList<ItemGroupDto>>;

public sealed class GetItemGroupsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetItemGroupsListQuery, IReadOnlyList<ItemGroupDto>>
{
    public async Task<IReadOnlyList<ItemGroupDto>> Handle(GetItemGroupsListQuery request, CancellationToken cancellationToken)
    {
        return await db.ItemGroups
            .AsNoTracking()
            .OrderBy(g => g.Code)
            .Select(g => new ItemGroupDto { Id = g.Id, Code = g.Code, NameAr = g.NameAr, NameEn = g.NameEn, ParentId = g.ParentId, IsActive = g.IsActive })
            .ToListAsync(cancellationToken);
    }
}
