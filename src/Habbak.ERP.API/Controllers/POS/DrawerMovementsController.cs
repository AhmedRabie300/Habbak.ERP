using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.DrawerMovements.Commands.ApproveDrawerMovement;
using Habbak.ERP.Application.POS.DrawerMovements.Commands.CreateDrawerMovement;
using Habbak.ERP.Application.POS.DrawerMovements.Commands.RejectDrawerMovement;
using Habbak.ERP.Application.POS.DrawerMovements.Queries.GetDrawerMovementsList;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/drawer-movements — screen #16 (05-Module-POS-Shifts.md): إيداع/سحب بسير اعتماد
/// (قاعدة 6/32).</summary>
[ApiController]
[Authorize]
[Screen("POS_DRAWER_MOVEMENTS", "POS_TABLE_BOARD")]
[Route("api/v1/pos/drawer-movements")]
public class DrawerMovementsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long ShiftId, DrawerMovementType MovementType, decimal Amount, string? Reason);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? shiftId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDrawerMovementsListQuery(shiftId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDrawerMovementCommand(request.ShiftId, request.MovementType, request.Amount, request.Reason), cancellationToken);
        return Ok(new { id });
    }

    [ScreenButton("POS_DRAWER_MOVEMENTS", "Approve")]
    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveDrawerMovementCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_DRAWER_MOVEMENTS", "Reject")]
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectDrawerMovementCommand(id), cancellationToken);
        return NoContent();
    }
}
