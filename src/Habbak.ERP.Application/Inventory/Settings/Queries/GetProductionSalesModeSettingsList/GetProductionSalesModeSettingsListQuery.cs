using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Settings.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Settings.Queries.GetProductionSalesModeSettingsList;

/// <summary>Screen #21's priority matrix — small enough (one row per configured scope) to return
/// unpaginated; the screen itself groups/sorts by ScopeType client-side to show the Item &gt; POS &gt;
/// Branch &gt; Company priority order (rule 17).</summary>
public sealed record GetProductionSalesModeSettingsListQuery : IRequest<IReadOnlyList<ProductionSalesModeSettingDto>>;

public sealed class GetProductionSalesModeSettingsListQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetProductionSalesModeSettingsListQuery, IReadOnlyList<ProductionSalesModeSettingDto>>
{
    public async Task<IReadOnlyList<ProductionSalesModeSettingDto>> Handle(
        GetProductionSalesModeSettingsListQuery request, CancellationToken cancellationToken)
    {
        return await db.ProductionSalesModeSettings
            .AsNoTracking()
            .Where(s => s.CompanyId == currentCompanyContext.CompanyId)
            .Select(s => new ProductionSalesModeSettingDto
            {
                Id = s.Id,
                ScopeType = s.ScopeType.ToString(),
                ScopeId = s.ScopeId,
                Mode = s.Mode.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}
