using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CancelInventoryCount;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CloseInventoryCount;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CompleteInventoryCountSettlement;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.CreateInventoryCount;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.RecordCountedQuantities;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.RejectInventoryCount;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.SettleInventoryCountLine;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.StartInventoryCount;
using Habbak.ERP.Application.Inventory.InventoryCounts.Commands.SubmitInventoryCountForSettlement;
using Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;
using Habbak.ERP.Application.Inventory.InventoryCounts.Queries.GetInventoryCountById;
using Habbak.ERP.Application.Inventory.InventoryCounts.Queries.GetInventoryCountsList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/inventory-counts — screen #14, the multi-stage count cycle
/// (02-Module-Inventory-Manufacturing.md, section 5): create → counters → settlement → close.</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_COUNTS")]
[Route("api/v1/inventory/inventory-counts")]
public class InventoryCountsController(ISender mediator) : ControllerBase
{
    public sealed record CreateInventoryCountRequest(long WarehouseId, DateOnly CountDate, InventoryCountType CountType, IReadOnlyList<long>? ItemIds);
    public sealed record RecordCountedQuantitiesRequest(IReadOnlyList<CountedQuantityInput> Lines);
    public sealed record SettleInventoryCountLineRequest(SettlementDecision Decision, string? SettlementReason);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetInventoryCountsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInventoryCountByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInventoryCountRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateInventoryCountCommand
        {
            WarehouseId = request.WarehouseId,
            CountDate = request.CountDate,
            CountType = request.CountType,
            ItemIds = request.ItemIds
        }, cancellationToken);

        return Ok(new { id });
    }

    [ScreenButton("INVENTORY_COUNTS", "Start")]
    [HttpPost("{id:long}/start")]
    public async Task<IActionResult> Start(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartInventoryCountCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/counted-quantities")]
    public async Task<IActionResult> RecordCountedQuantities(long id, [FromBody] RecordCountedQuantitiesRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecordCountedQuantitiesCommand { Id = id, Lines = request.Lines }, cancellationToken);
        return NoContent();
    }

    [ScreenButton("INVENTORY_COUNTS", "SubmitForSettlement")]
    [HttpPost("{id:long}/submit-for-settlement")]
    public async Task<IActionResult> SubmitForSettlement(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitInventoryCountForSettlementCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("INVENTORY_COUNTS", "SettleLine")]
    [HttpPost("{id:long}/lines/{lineId:long}/settle")]
    public async Task<IActionResult> SettleLine(long id, long lineId, [FromBody] SettleInventoryCountLineRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SettleInventoryCountLineCommand
        {
            Id = id,
            LineId = lineId,
            Decision = request.Decision,
            SettlementReason = request.SettlementReason
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("INVENTORY_COUNTS", "CompleteSettlement")]
    [HttpPost("{id:long}/complete-settlement")]
    public async Task<IActionResult> CompleteSettlement(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteInventoryCountSettlementCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("INVENTORY_COUNTS", "Close")]
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CloseInventoryCountCommand(id, IdempotencyHeader.Read(this)), cancellationToken));

    [ScreenButton("INVENTORY_COUNTS", "Reject")]
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectInventoryCountCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("INVENTORY_COUNTS", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelInventoryCountCommand(id), cancellationToken);
        return NoContent();
    }
}
