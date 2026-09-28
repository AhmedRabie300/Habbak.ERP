using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShortagePolicyEntity = Habbak.ERP.Domain.Inventory.ShortagePolicy;

namespace Habbak.ERP.Application.Inventory.Settings.Commands.UpdateShortagePolicy;

/// <summary>Screen #22 — upserts the single ShortagePolicy row for the current company.</summary>
public sealed record UpdateShortagePolicyCommand(bool AllowOverrideOnShortage, bool RequiresApprovalForOverride) : IRequest;

public sealed class UpdateShortagePolicyCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateShortagePolicyCommand>
{
    public async Task Handle(UpdateShortagePolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await db.ShortagePolicies
            .FirstOrDefaultAsync(p => p.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (policy is null)
        {
            policy = new ShortagePolicyEntity { CompanyId = currentCompanyContext.CompanyId };
            db.ShortagePolicies.Add(policy);
        }

        policy.AllowOverrideOnShortage = request.AllowOverrideOnShortage;
        policy.RequiresApprovalForOverride = request.RequiresApprovalForOverride;

        await db.SaveChangesAsync(cancellationToken);
    }
}
