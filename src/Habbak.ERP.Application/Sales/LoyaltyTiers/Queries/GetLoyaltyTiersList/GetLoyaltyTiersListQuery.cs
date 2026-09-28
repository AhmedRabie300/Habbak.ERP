using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Queries.GetLoyaltyTiersList;

public sealed record GetLoyaltyTiersListQuery : IRequest<IReadOnlyList<LoyaltyTierListItemDto>>;

public sealed class GetLoyaltyTiersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetLoyaltyTiersListQuery, IReadOnlyList<LoyaltyTierListItemDto>>
{
    public async Task<IReadOnlyList<LoyaltyTierListItemDto>> Handle(GetLoyaltyTiersListQuery request, CancellationToken cancellationToken)
    {
        return await db.LoyaltyTiers
            .AsNoTracking()
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new LoyaltyTierListItemDto
            {
                Id = t.Id,
                Code = t.Code,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                DisplayOrder = t.DisplayOrder,
                MinPointsThreshold = t.MinPointsThreshold,
                EarnRateMultiplier = t.EarnRateMultiplier,
                IsActive = t.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
