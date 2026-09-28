using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.LoyaltyTiers.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.LoyaltyTiers.Queries.GetLoyaltyTierById;

public sealed record GetLoyaltyTierByIdQuery(long Id) : IRequest<LoyaltyTierDetailDto>;

public sealed class GetLoyaltyTierByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLoyaltyTierByIdQuery, LoyaltyTierDetailDto>
{
    public async Task<LoyaltyTierDetailDto> Handle(GetLoyaltyTierByIdQuery request, CancellationToken cancellationToken)
    {
        var tier = await db.LoyaltyTiers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(LoyaltyTier), request.Id);

        return new LoyaltyTierDetailDto
        {
            Id = tier.Id,
            Code = tier.Code,
            NameAr = tier.NameAr,
            NameEn = tier.NameEn,
            DisplayOrder = tier.DisplayOrder,
            MinPointsThreshold = tier.MinPointsThreshold,
            EarnRateMultiplier = tier.EarnRateMultiplier,
            IsActive = tier.IsActive
        };
    }
}
