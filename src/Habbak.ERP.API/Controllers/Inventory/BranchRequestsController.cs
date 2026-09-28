using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.BranchRequests.Commands.ApproveBranchRequest;
using Habbak.ERP.Application.Inventory.BranchRequests.Commands.CreateBranchRequest;
using Habbak.ERP.Application.Inventory.BranchRequests.Commands.SubmitBranchRequest;
using Habbak.ERP.Application.Inventory.BranchRequests.Commands.UpdateBranchRequest;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Application.Inventory.BranchRequests.Queries.GetBranchRequestById;
using Habbak.ERP.Application.Inventory.BranchRequests.Queries.GetBranchRequestsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/branch-requests — screens #12-13 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_BRANCH_REQUEST")]
[Route("api/v1/inventory/branch-requests")]
public class BranchRequestsController(ISender mediator) : ControllerBase
{
    public sealed record CreateBranchRequestRequest(long BranchId, DateOnly RequestDate, IReadOnlyList<BranchRequestLineInput> Lines);
    public sealed record UpdateBranchRequestRequest(string RowVersion, DateOnly RequestDate, IReadOnlyList<BranchRequestLineInput> Lines);
    public sealed record ApproveBranchRequestRequest(
        long SourceWarehouseId, long CustodyOfficerId, DateOnly TransferDocumentDate, IReadOnlyList<ApproveBranchRequestLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetBranchRequestsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBranchRequestByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequestRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateBranchRequestCommand
        {
            BranchId = request.BranchId,
            RequestDate = request.RequestDate,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateBranchRequestRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBranchRequestCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            RequestDate = request.RequestDate,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitBranchRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, [FromBody] ApproveBranchRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ApproveBranchRequestCommand
        {
            Id = id,
            SourceWarehouseId = request.SourceWarehouseId,
            CustodyOfficerId = request.CustodyOfficerId,
            TransferDocumentDate = request.TransferDocumentDate,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(result);
    }
}
