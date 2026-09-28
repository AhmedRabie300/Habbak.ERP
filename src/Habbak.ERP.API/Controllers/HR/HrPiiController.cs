using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.HR.Employees.Commands.RevealPiiField;
using Habbak.ERP.Application.Settings.Access;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1b, Batch B5 — the one path to an employee's plaintext
/// National ID/IBAN. [Screen("HR_EMPLOYEES")] alone would only require the ordinary View permission;
/// [ScreenButton("HR_EMPLOYEES", "RevealPii")] on the action requires the separate HR_REVEAL_PII
/// button permission on top of it (ButtonPermissionCatalog, fallback Approve) — a role with plain
/// View/Edit on HR_EMPLOYEES cannot reveal PII until that button permission is explicitly granted.
/// </summary>
[ApiController]
[Authorize]
[Screen("HR_EMPLOYEES")]
[Route("api/v1/hr/pii")]
public class HrPiiController(ISender mediator) : ControllerBase
{
    public sealed record RevealPiiRequest(string EntityType, long EntityId, string FieldName);

    [HttpPost("reveal")]
    [ScreenButton("HR_EMPLOYEES", "RevealPii")]
    [ScreenAction(ScreenAction.Approve)]
    public async Task<IActionResult> Reveal([FromBody] RevealPiiRequest request, CancellationToken cancellationToken)
    {
        var value = await mediator.Send(new RevealPiiFieldCommand(request.EntityType, request.EntityId, request.FieldName), cancellationToken);
        return Ok(new { value });
    }
}
