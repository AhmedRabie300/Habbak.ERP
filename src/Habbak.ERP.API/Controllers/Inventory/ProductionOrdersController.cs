using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CancelProductionOrder;
using Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CompleteProductionOrder;
using Habbak.ERP.Application.Inventory.ProductionOrders.Commands.CreateProductionOrder;
using Habbak.ERP.Application.Inventory.ProductionOrders.Commands.StartProductionOrder;
using Habbak.ERP.Application.Inventory.ProductionOrders.Commands.UpdateProductionOrder;
using Habbak.ERP.Application.Inventory.ProductionOrders.Queries.GetProductionOrderById;
using Habbak.ERP.Application.Inventory.ProductionOrders.Queries.GetProductionOrdersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/production-orders — screen #19 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_PRODUCTION_ORDERS")]
[Route("api/v1/inventory/production-orders")]
public class ProductionOrdersController(ISender mediator) : ControllerBase
{
    public sealed record CreateProductionOrderRequest(long WarehouseId, long RecipeId, decimal PlannedQuantity, DateOnly? StartDate);
    public sealed record UpdateProductionOrderRequest(string RowVersion, long WarehouseId, long RecipeId, decimal PlannedQuantity, DateOnly? StartDate);
    public sealed record CompleteProductionOrderRequest(decimal ActualQuantity, DateOnly EndDate, decimal? ActualWasteQuantity, string? WasteReason);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetProductionOrdersListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetProductionOrderByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateProductionOrderCommand
        {
            WarehouseId = request.WarehouseId,
            RecipeId = request.RecipeId,
            PlannedQuantity = request.PlannedQuantity,
            StartDate = request.StartDate
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateProductionOrderCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            WarehouseId = request.WarehouseId,
            RecipeId = request.RecipeId,
            PlannedQuantity = request.PlannedQuantity,
            StartDate = request.StartDate
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("INVENTORY_PRODUCTION_ORDERS", "Start")]
    [HttpPost("{id:long}/start")]
    public async Task<IActionResult> Start(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartProductionOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("INVENTORY_PRODUCTION_ORDERS", "Complete")]
    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CompleteProductionOrderCommand
        {
            Id = id,
            ActualQuantity = request.ActualQuantity,
            EndDate = request.EndDate,
            ActualWasteQuantity = request.ActualWasteQuantity,
            WasteReason = request.WasteReason
        }, cancellationToken);

        return Ok(result);
    }

    [ScreenButton("INVENTORY_PRODUCTION_ORDERS", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelProductionOrderCommand(id), cancellationToken);
        return NoContent();
    }
}
