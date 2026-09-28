using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.CreateCustodyOfficer;
using Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.DeleteCustodyOfficer;
using Habbak.ERP.Application.Inventory.CustodyOfficers.Commands.UpdateCustodyOfficer;
using Habbak.ERP.Application.Inventory.CustodyOfficers.Queries.GetCustodyOfficersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/custody-officers — screen #6 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_CUSTODY_OFFICERS", LookupReads = true)]
[Route("api/v1/inventory/custody-officers")]
public class CustodyOfficersController(ISender mediator) : ControllerBase
{
    public sealed record CreateCustodyOfficerRequest(string? Code, string NameAr, string NameEn, long? BranchId);
    public sealed record UpdateCustodyOfficerRequest(string NameAr, string NameEn, long? BranchId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustodyOfficersListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustodyOfficerRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCustodyOfficerCommand(request.Code, request.NameAr, request.NameEn, request.BranchId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustodyOfficerRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCustodyOfficerCommand(id, request.NameAr, request.NameEn, request.BranchId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCustodyOfficerCommand(id), cancellationToken);
        return NoContent();
    }
}
