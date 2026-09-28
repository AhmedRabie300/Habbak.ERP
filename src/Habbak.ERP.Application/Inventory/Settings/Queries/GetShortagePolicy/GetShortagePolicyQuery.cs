using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Settings.Queries.GetShortagePolicy;

/// <summary>Screen #22 — one row per company; returns safe defaults (no override allowed) when no
/// row has been configured yet, rather than 404ing a settings screen that should just show its
/// out-of-the-box behavior.</summary>
public sealed record GetShortagePolicyQuery : IRequest<ShortagePolicyDto>;

public sealed class GetShortagePolicyQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetShortagePolicyQuery, ShortagePolicyDto>
{
    public async Task<ShortagePolicyDto> Handle(GetShortagePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await db.ShortagePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        return policy is null
            ? new ShortagePolicyDto(false, false)
            : new ShortagePolicyDto(policy.AllowOverrideOnShortage, policy.RequiresApprovalForOverride);
    }
}
