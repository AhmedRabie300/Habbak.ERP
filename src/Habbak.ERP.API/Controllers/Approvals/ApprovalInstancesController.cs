using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Approvals.Commands;
using Habbak.ERP.Application.Approvals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Approvals;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12 — approving/rejecting a pending step, and the "بانتظار
/// اعتمادي" worklist (00-Frontend-Specs.md §19). Two screens on purpose: an ordinary approver only
/// ever needs APPROVAL_MY_PENDING; ReassignInstance is an admin override gated by the
/// SETTINGS_APPROVAL_WORKFLOWS/ManualReassign button permission specifically (§12.4 rule 10).
/// </summary>
[ApiController]
[Authorize]
[Screen("APPROVAL_MY_PENDING", "SETTINGS_APPROVAL_WORKFLOWS")]
[Route("api/v1/approvals/instances")]
public class ApprovalInstancesController(ISender mediator) : ControllerBase
{
    public sealed record RejectInstanceRequest(string Reason);
    public sealed record ApproveInstanceRequest(string? Reason);
    public sealed record ReassignInstanceRequest(long NewApproverUserId, string? Reason);

    [HttpGet("pending-for-me")]
    public async Task<IActionResult> GetPendingForMe(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMyPendingApprovalsQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetApprovalInstanceByIdQuery(id), cancellationToken));

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string entityType, [FromQuery] long entityId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetApprovalHistoryQuery(entityType, entityId), cancellationToken));

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveInstanceRequest? request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveStepCommand(id, request?.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectInstanceRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectStepCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reassign")]
    [ScreenButton("SETTINGS_APPROVAL_WORKFLOWS", "ManualReassign")]
    public async Task<IActionResult> Reassign(long id, [FromBody] ReassignInstanceRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReassignInstanceCommand(id, request.NewApproverUserId, request.Reason), cancellationToken);
        return NoContent();
    }
}
