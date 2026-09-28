using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.Settings.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Settings.Queries.GetLoyaltyProgramSettings;

public sealed record GetLoyaltyProgramSettingsQuery : IRequest<LoyaltyProgramSettingsDto>;

public sealed class GetLoyaltyProgramSettingsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetLoyaltyProgramSettingsQuery, LoyaltyProgramSettingsDto>
{
    public async Task<LoyaltyProgramSettingsDto> Handle(GetLoyaltyProgramSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.LoyaltyProgramSettingsRows
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken)
            ?? new LoyaltyProgramSettings();

        return new LoyaltyProgramSettingsDto
        {
            PointsEarnRate = settings.PointsEarnRate,
            PointsRedemptionValue = settings.PointsRedemptionValue
        };
    }
}
