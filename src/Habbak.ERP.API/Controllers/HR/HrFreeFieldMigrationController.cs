using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.HR.Migration;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.2, sub-batch 1.2.2 — no dedicated screen exists for
/// this yet (the interactive linking screen was deferred; see Phase-1.2-Research.md §6 question 1), so
/// this reuses HR_EMPLOYEES: GetReport needs plain View, ApplyAutoMatch needs Edit (default POST rule),
/// both already meaningful permissions for whoever manages employees.
/// </summary>
[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[Route("api/v1/hr/free-field-migration")]
public class HrFreeFieldMigrationController(ISender mediator) : ControllerBase
{
    [HttpGet("diagnostic-report")]
    public async Task<IActionResult> GetDiagnosticReport(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetFreeFieldDiagnosticReportQuery(), cancellationToken));

    [HttpPost("apply-auto-match")]
    public async Task<IActionResult> ApplyAutoMatch(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ApplyFreeFieldAutoMatchCommand(), cancellationToken));
}
