using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.Warehouses.Commands.CreateWarehouse;
using Habbak.ERP.Application.Inventory.Warehouses.Commands.DeleteWarehouse;
using Habbak.ERP.Application.Inventory.Warehouses.Commands.UpdateWarehouse;
using Habbak.ERP.Application.Inventory.Warehouses.Queries.GetWarehousesList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/warehouses — dedicated reference list (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_WAREHOUSES", LookupReads = true)]
[Route("api/v1/inventory/warehouses")]
public class WarehousesController(ISender mediator) : ControllerBase
{
    public sealed record CreateWarehouseRequest(string? Code, string NameAr, string NameEn, WarehouseType WarehouseType, long? BranchId, bool AllowNegativeBalance);
    public sealed record UpdateWarehouseRequest(string NameAr, string NameEn, WarehouseType WarehouseType, long? BranchId, bool AllowNegativeBalance, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetWarehousesListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateWarehouseCommand(request.Code, request.NameAr, request.NameEn, request.WarehouseType, request.BranchId, request.AllowNegativeBalance), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateWarehouseCommand(id, request.NameAr, request.NameEn, request.WarehouseType, request.BranchId, request.AllowNegativeBalance, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteWarehouseCommand(id), cancellationToken);
        return NoContent();
    }
}
