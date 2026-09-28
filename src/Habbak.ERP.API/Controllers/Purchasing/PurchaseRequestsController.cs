using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.ApprovePurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.CancelPurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.CreatePurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.RejectPurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.SubmitPurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Commands.UpdatePurchaseRequest;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetApprovedPurchaseRequestsList;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetOpenPurchaseRequests;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestById;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestLinesForOrder;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestsList;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/purchase-requests — screen #2 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_PURCHASE_REQUESTS")]
[Route("api/v1/purchasing/purchase-requests")]
public class PurchaseRequestsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePurchaseRequestRequest(
        long BranchId, DateOnly RequestDate, PurchaseRequestPriority Priority, string? Reason, string? Notes,
        IReadOnlyList<PurchaseRequestLineInput> Lines);

    public sealed record UpdatePurchaseRequestRequest(
        string RowVersion, DateOnly RequestDate, PurchaseRequestPriority Priority, string? Reason, string? Notes,
        IReadOnlyList<PurchaseRequestLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseRequestsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>Feeds screen #4's "convert to purchase order" picker.</summary>
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetApprovedPurchaseRequestsListQuery(), cancellationToken));

    /// <summary>The requests that still have something left to convert to an order (Remarks7).</summary>
    [HttpGet("open-for-order")]
    public async Task<IActionResult> GetOpenForOrder(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOpenPurchaseRequestsQuery(), cancellationToken));

    /// <summary>This request's lines with what is still convertible on each — what the order screen loads (Remarks7).</summary>
    [HttpGet("{id:long}/order-lines")]
    public async Task<IActionResult> GetOrderLines(long id, [FromQuery] long? excludeOrderId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseRequestLinesForOrderQuery(id, excludeOrderId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseRequestByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseRequestCommand
        {
            BranchId = request.BranchId,
            RequestDate = request.RequestDate,
            Priority = request.Priority,
            Reason = request.Reason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePurchaseRequestRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseRequestCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            RequestDate = request.RequestDate,
            Priority = request.Priority,
            Reason = request.Reason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApprovePurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseRequestCommand(id), cancellationToken);
        return NoContent();
    }
}
