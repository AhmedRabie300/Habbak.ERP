using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Settings.Queries.GetInventorySettings;

/// <summary>Screen #23 — one row per company; rule 36's default of 90 days applies when no row
/// has been configured yet.</summary>
public sealed record GetInventorySettingsQuery : IRequest<InventorySettingsDto>;

public sealed class GetInventorySettingsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetInventorySettingsQuery, InventorySettingsDto>
{
    public async Task<InventorySettingsDto> Handle(GetInventorySettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.InventorySettingsRows
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        return new InventorySettingsDto(settings?.SlowMovingThresholdDays ?? 90);
    }
}
