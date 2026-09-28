using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Terminals.Commands.CreatePOSTerminal;
using Habbak.ERP.Application.POS.Terminals.Commands.DeletePOSTerminal;
using Habbak.ERP.Application.POS.Terminals.Commands.UpdatePOSTerminal;
using Habbak.ERP.Application.POS.Terminals.Queries.GetPOSTerminalById;
using Habbak.ERP.Application.POS.Terminals.Queries.GetPOSTerminalsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/terminals — screen مبدئي (05-Module-POS-Shifts.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("POS_TERMINALS", LookupReads = true)]
[Route("api/v1/pos/terminals")]
public class POSTerminalsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePOSTerminalRequest(string? Code, string NameAr, string NameEn, long BranchId, long? DefaultWarehouseId);

    public sealed record UpdatePOSTerminalRequest(string NameAr, string NameEn, long BranchId, long? DefaultWarehouseId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSTerminalsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSTerminalByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePOSTerminalRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePOSTerminalCommand(request.Code, request.NameAr, request.NameEn, request.BranchId, request.DefaultWarehouseId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePOSTerminalRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePOSTerminalCommand(id, request.NameAr, request.NameEn, request.BranchId, request.DefaultWarehouseId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePOSTerminalCommand(id), cancellationToken);
        return NoContent();
    }
}
