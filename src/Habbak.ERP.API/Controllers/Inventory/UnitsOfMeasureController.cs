using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.CreateUnitOfMeasure;
using Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.DeleteUnitOfMeasure;
using Habbak.ERP.Application.Inventory.UnitsOfMeasure.Commands.UpdateUnitOfMeasure;
using Habbak.ERP.Application.Inventory.UnitsOfMeasure.Queries.GetUnitsOfMeasureList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/units-of-measure — dedicated reference list (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_UNITS_OF_MEASURE", LookupReads = true)]
[Route("api/v1/inventory/units-of-measure")]
public class UnitsOfMeasureController(ISender mediator) : ControllerBase
{
    public sealed record CreateUnitOfMeasureRequest(string? Code, string NameAr, string NameEn, UnitCategory Category);
    public sealed record UpdateUnitOfMeasureRequest(string NameAr, string NameEn, UnitCategory Category, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUnitsOfMeasureListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUnitOfMeasureRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateUnitOfMeasureCommand(request.Code, request.NameAr, request.NameEn, request.Category), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateUnitOfMeasureRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateUnitOfMeasureCommand(id, request.NameAr, request.NameEn, request.Category, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUnitOfMeasureCommand(id), cancellationToken);
        return NoContent();
    }
}
