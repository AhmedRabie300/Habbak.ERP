using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Sales.SalesReturns.Commands.CancelSalesReturn;
using Habbak.ERP.Application.Sales.SalesReturns.Commands.CreateSalesReturn;
using Habbak.ERP.Application.Sales.SalesReturns.Commands.PostSalesReturn;
using Habbak.ERP.Application.Sales.SalesReturns.Commands.RejectSalesReturn;
using Habbak.ERP.Application.Sales.SalesReturns.Commands.UpdateSalesReturn;
using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Application.Sales.SalesReturns.Queries.GetSalesReturnById;
using Habbak.ERP.Application.Sales.SalesReturns.Queries.GetSalesReturnsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/returns — screen #9 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_RETURNS")]
[Route("api/v1/sales/returns")]
public class SalesReturnsController(ISender mediator) : ControllerBase
{
    public sealed record CreateSalesReturnRequest(
        long? BranchId, long CustomerId, long WarehouseId, DateOnly ReturnDate,
        long? SourceInvoiceId, string Reason, IReadOnlyList<SalesReturnLineInput> Lines);

    public sealed record UpdateSalesReturnRequest(
        string RowVersion, long? BranchId, long CustomerId, long WarehouseId, DateOnly ReturnDate,
        string Reason, IReadOnlyList<SalesReturnLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSalesReturnsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSalesReturnByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesReturnRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalesReturnCommand
        {
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            ReturnDate = request.ReturnDate,
            SourceInvoiceId = request.SourceInvoiceId,
            Reason = request.Reason,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSalesReturnRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalesReturnCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            ReturnDate = request.ReturnDate,
            Reason = request.Reason,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostSalesReturnCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectSalesReturnCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSalesReturnCommand(id), cancellationToken);
        return NoContent();
    }
}
