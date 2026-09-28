using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Settings.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Settings.Queries.GetHrSettings;

/// <summary>Screen HR_SETTINGS — one row per company; returns the entity's own defaults
/// (DefaultProbationDays=90, RequireNationalIdForActivation=true) when no row has been configured yet.</summary>
public sealed record GetHrSettingsQuery : IRequest<HrSettingsDto>;

public sealed class GetHrSettingsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetHrSettingsQuery, HrSettingsDto>
{
    public async Task<HrSettingsDto> Handle(GetHrSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.HrSettingsRows
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken)
            ?? new HrSettings();

        return new HrSettingsDto
        {
            DefaultProbationDays = settings.DefaultProbationDays,
            DefaultBranchId = settings.DefaultBranchId,
            RequireNationalIdForActivation = settings.RequireNationalIdForActivation,
            LeaveDayCountingMode = settings.LeaveDayCountingMode,
            MonthBasis = settings.MonthBasis,
            DefaultCutoffDay = settings.DefaultCutoffDay,
            CompanyDefaultApproverUserId = settings.CompanyDefaultApproverUserId
        };
    }
}
