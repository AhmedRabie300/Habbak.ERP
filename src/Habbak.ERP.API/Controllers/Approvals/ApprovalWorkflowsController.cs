using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Approvals;
using Habbak.ERP.Application.Approvals.Commands;
using Habbak.ERP.Application.Approvals.Queries;
using Habbak.ERP.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Approvals;

/// <summary>Docs/Modules/00-Project-Overview.md §12 — settings screen for approval chains ("سلاسل الاعتماد").</summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_APPROVAL_WORKFLOWS")]
[Route("api/v1/approvals/workflows")]
public class ApprovalWorkflowsController(ISender mediator) : ControllerBase
{
    public sealed record CreateApprovalWorkflowRequest(string Code, string NameAr, string NameEn, IReadOnlyList<ApprovalWorkflowStepInput> Steps);
    public sealed record UpdateApprovalWorkflowRequest(string Code, string NameAr, string NameEn, IReadOnlyList<ApprovalWorkflowStepInput> Steps);
    public sealed record AssignWorkflowRequest(long ScreenId, long ApprovalWorkflowId, decimal? MinAmount);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetApprovalWorkflowsListQuery { Search = query.Search, Page = query.Page, PageSize = query.PageSize, SortBy = query.SortBy, SortDir = query.SortDir }, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetApprovalWorkflowByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApprovalWorkflowRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateApprovalWorkflowCommand(
            new ApprovalWorkflowDefinition(request.Code, request.NameAr, request.NameEn, request.Steps)), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateApprovalWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateApprovalWorkflowCommand(
            id, new ApprovalWorkflowDefinition(request.Code, request.NameAr, request.NameEn, request.Steps)), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetApprovalWorkflowActiveCommand(id, true), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetApprovalWorkflowActiveCommand(id, false), cancellationToken);
        return NoContent();
    }

    [HttpGet("assignments")]
    public async Task<IActionResult> GetAssignments(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetWorkflowAssignmentsListQuery(), cancellationToken));

    [HttpPost("assignments")]
    public async Task<IActionResult> Assign([FromBody] AssignWorkflowRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new AssignWorkflowToScreenCommand(request.ScreenId, request.ApprovalWorkflowId, request.MinAmount), cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("assignments/{screenId:long}")]
    public async Task<IActionResult> Unassign(long screenId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnassignWorkflowFromScreenCommand(screenId), cancellationToken);
        return NoContent();
    }
}
