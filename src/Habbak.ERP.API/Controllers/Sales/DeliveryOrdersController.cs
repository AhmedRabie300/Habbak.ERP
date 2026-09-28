using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Sales.DeliveryOrders.Commands.CreateDeliveryOrder;
using Habbak.ERP.Application.Sales.DeliveryOrders.Commands.PostDeliveryOrder;
using Habbak.ERP.Application.Sales.DeliveryOrders.Commands.RejectDeliveryOrder;
using Habbak.ERP.Application.Sales.DeliveryOrders.Commands.UpdateDeliveryOrder;
using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Application.Sales.DeliveryOrders.Queries.GetDeliveryOrderById;
using Habbak.ERP.Application.Sales.DeliveryOrders.Queries.GetDeliveryOrdersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/delivery-orders — screen #8 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_DELIVERY_ORDERS")]
[Route("api/v1/sales/delivery-orders")]
public class DeliveryOrdersController(ISender mediator) : ControllerBase
{
    public sealed record CreateDeliveryOrderRequest(
        long? BranchId, long CustomerId, long WarehouseId, DateOnly DeliveryDate,
        long? SourceOrderId, long? SourceInvoiceId, IReadOnlyList<DeliveryOrderLineInput> Lines);

    public sealed record UpdateDeliveryOrderRequest(
        string RowVersion, long? BranchId, long WarehouseId, DateOnly DeliveryDate, IReadOnlyList<DeliveryOrderLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetDeliveryOrdersListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDeliveryOrderByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDeliveryOrderCommand
        {
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            DeliveryDate = request.DeliveryDate,
            SourceOrderId = request.SourceOrderId,
            SourceInvoiceId = request.SourceInvoiceId,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateDeliveryOrderRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateDeliveryOrderCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            WarehouseId = request.WarehouseId,
            DeliveryDate = request.DeliveryDate,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostDeliveryOrderCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectDeliveryOrderCommand(id), cancellationToken);
        return NoContent();
    }
}
