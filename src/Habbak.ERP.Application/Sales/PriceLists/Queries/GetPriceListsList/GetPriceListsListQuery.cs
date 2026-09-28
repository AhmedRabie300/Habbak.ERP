using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.PriceLists.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.PriceLists.Queries.GetPriceListsList;

public sealed record GetPriceListsListQuery : IRequest<IReadOnlyList<PriceListListItemDto>>;

public sealed class GetPriceListsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPriceListsListQuery, IReadOnlyList<PriceListListItemDto>>
{
    public async Task<IReadOnlyList<PriceListListItemDto>> Handle(GetPriceListsListQuery request, CancellationToken cancellationToken)
    {
        return await db.PriceLists
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new PriceListListItemDto
            {
                Id = p.Id,
                Code = p.Code,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                EffectiveFromDate = p.EffectiveFromDate,
                EffectiveToDate = p.EffectiveToDate,
                BranchCount = p.Branches.Count,
                ItemCount = p.Lines.Count,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
