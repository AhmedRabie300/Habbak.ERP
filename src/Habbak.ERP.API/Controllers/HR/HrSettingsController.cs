using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.HR.Settings.Commands.UpdateHrSettings;
using Habbak.ERP.Application.HR.Settings.Queries.GetHrSettings;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>/hr/settings — Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.0
/// (+ Phase 3: LeaveDayCountingMode).</summary>
[ApiController]
[Authorize]
[Screen("HR_SETTINGS")]
[Route("api/v1/hr/settings")]
public class HrSettingsController(ISender mediator) : ControllerBase
{
    public sealed record UpdateHrSettingsRequest(
        int DefaultProbationDays, long? DefaultBranchId, bool RequireNationalIdForActivation, LeaveDayCountingMode LeaveDayCountingMode,
        HrMonthBasis? MonthBasis, int? DefaultCutoffDay, long? CompanyDefaultApproverUserId);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetHrSettingsQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateHrSettingsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateHrSettingsCommand
        {
            DefaultProbationDays = request.DefaultProbationDays,
            DefaultBranchId = request.DefaultBranchId,
            RequireNationalIdForActivation = request.RequireNationalIdForActivation,
            LeaveDayCountingMode = request.LeaveDayCountingMode,
            MonthBasis = request.MonthBasis,
            DefaultCutoffDay = request.DefaultCutoffDay,
            CompanyDefaultApproverUserId = request.CompanyDefaultApproverUserId
        }, cancellationToken);

        return NoContent();
    }
}
