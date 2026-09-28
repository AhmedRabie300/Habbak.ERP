using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.SalesOrders.Commands.CancelSalesOrder;
using Habbak.ERP.Application.Sales.SalesOrders.Commands.ConfirmSalesOrder;
using Habbak.ERP.Application.Sales.SalesOrders.Commands.CreateSalesOrder;
using Habbak.ERP.Application.Sales.SalesOrders.Commands.RejectSalesOrder;
using Habbak.ERP.Application.Sales.SalesOrders.Commands.UpdateSalesOrder;
using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Application.Sales.SalesOrders.Queries.GetConfirmedSalesOrdersList;
using Habbak.ERP.Application.Sales.SalesOrders.Queries.GetSalesOrderById;
using Habbak.ERP.Application.Sales.SalesOrders.Queries.GetSalesOrdersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/sales-orders — screen #6 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_ORDERS")]
[Route("api/v1/sales/sales-orders")]
public class SalesOrdersController(ISender mediator) : ControllerBase
{
    public sealed record CreateSalesOrderRequest(
        long? BranchId, long CustomerId, DateOnly OrderDate, long? SourceQuoteId, IReadOnlyList<SalesOrderLineInput> Lines);

    public sealed record UpdateSalesOrderRequest(
        string RowVersion, long? BranchId, long CustomerId, DateOnly OrderDate, IReadOnlyList<SalesOrderLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSalesOrdersListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>Feeds screen #7's "convert to sales invoice" picker.</summary>
    [HttpGet("confirmed")]
    public async Task<IActionResult> GetConfirmed(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetConfirmedSalesOrdersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSalesOrderByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSalesOrderCommand
        {
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            OrderDate = request.OrderDate,
            SourceQuoteId = request.SourceQuoteId,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSalesOrderCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            OrderDate = request.OrderDate,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ConfirmSalesOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectSalesOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSalesOrderCommand(id), cancellationToken);
        return NoContent();
    }
}
