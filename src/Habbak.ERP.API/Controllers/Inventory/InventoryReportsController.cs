using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/reports — My Remarks/Remarks2.md bug 1.3 ("لا يحتوي على أي تقارير حالياً")
/// and remark 3.8's 8-report list, matching the /purchasing/reports hub pattern exactly.</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_REPORTS")]
[Route("api/v1/inventory/reports")]
public class InventoryReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("item-movement")]
    public async Task<IActionResult> ItemMovement(
        [FromQuery] long itemId, [FromQuery] long? warehouseId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetItemMovementReportQuery(itemId, warehouseId, from, to), cancellationToken));

    [HttpGet("stock-report")]
    public async Task<IActionResult> StockReport(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetStockReportQuery(), cancellationToken));

    [HttpGet("below-minimum-items")]
    public async Task<IActionResult> BelowMinimumItems(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBelowMinimumItemsReportQuery(), cancellationToken));

    [HttpGet("inventory-counts")]
    public async Task<IActionResult> InventoryCounts(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInventoryCountsReportQuery(), cancellationToken));

    [HttpGet("daily-movement")]
    public async Task<IActionResult> DailyMovement([FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDailyMovementReportQuery(date), cancellationToken));

    [HttpGet("raw-material-consumption")]
    public async Task<IActionResult> RawMaterialConsumption([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetRawMaterialConsumptionReportQuery(from, to), cancellationToken));

    [HttpGet("waste")]
    public async Task<IActionResult> Waste(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetWasteReportQuery(), cancellationToken));

    [HttpGet("stock-transfers")]
    public async Task<IActionResult> StockTransfers([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetStockTransfersReportQuery(from, to), cancellationToken));
}
