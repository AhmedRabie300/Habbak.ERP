using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.CancelPurchaseReturn;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.CreatePurchaseReturn;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.PostPurchaseReturn;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.UpdatePurchaseReturn;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetInvoiceLinesForReturn;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnById;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnsList;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/purchase-returns — screen #7 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_PURCHASE_RETURNS")]
[Route("api/v1/purchasing/purchase-returns")]
public class PurchaseReturnsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePurchaseReturnRequest(
        long? BranchId, DateOnly ReturnDate, long SupplierId, long? PurchaseInvoiceId, long WarehouseId,
        PurchaseReturnReason Reason, string? Notes, IReadOnlyList<PurchaseReturnLineInput> Lines);

    public sealed record UpdatePurchaseReturnRequest(
        string RowVersion, long? BranchId, DateOnly ReturnDate, long SupplierId, long? PurchaseInvoiceId, long WarehouseId,
        PurchaseReturnReason Reason, string? Notes, IReadOnlyList<PurchaseReturnLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseReturnsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>The chosen invoice's lines with what is still returnable on each (Remarks4, item 8).</summary>
    [HttpGet("invoice-lines")]
    public async Task<IActionResult> GetInvoiceLines(
        [FromQuery] long invoiceId, [FromQuery] long? excludeReturnId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInvoiceLinesForReturnQuery(invoiceId, excludeReturnId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseReturnByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseReturnRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseReturnCommand
        {
            BranchId = request.BranchId,
            ReturnDate = request.ReturnDate,
            SupplierId = request.SupplierId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePurchaseReturnRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseReturnCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            ReturnDate = request.ReturnDate,
            SupplierId = request.SupplierId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostPurchaseReturnCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseReturnCommand(id), cancellationToken);
        return NoContent();
    }
}
