using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.WasteRecords.Commands.CreateWasteRecord;
using Habbak.ERP.Application.Inventory.WasteRecords.Commands.UpdateWasteRecord;
using Habbak.ERP.Application.Inventory.WasteRecords.Queries.GetWasteRecordById;
using Habbak.ERP.Application.Inventory.WasteRecords.Queries.GetWasteRecordsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/waste-records — screen #18 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_WASTE_RECORDS")]
[Route("api/v1/inventory/waste-records")]
public class WasteRecordsController(ISender mediator) : ControllerBase
{
    public sealed record CreateWasteRecordRequest(long WarehouseId, long ItemId, decimal Quantity, DateOnly WasteDate, string Reason);
    public sealed record UpdateWasteRecordRequest(string RowVersion, DateOnly WasteDate, string Reason);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetWasteRecordsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetWasteRecordByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWasteRecordRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateWasteRecordCommand
        {
            WarehouseId = request.WarehouseId,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            WasteDate = request.WasteDate,
            Reason = request.Reason
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateWasteRecordRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateWasteRecordCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            WasteDate = request.WasteDate,
            Reason = request.Reason
        }, cancellationToken);

        return NoContent();
    }
}
