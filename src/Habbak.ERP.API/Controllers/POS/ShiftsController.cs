using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Shifts.Commands.ApproveShiftClose;
using Habbak.ERP.Application.POS.Shifts.Commands.CloseShift;
using Habbak.ERP.Application.POS.Shifts.Commands.OpenShift;
using Habbak.ERP.Application.POS.Shifts.Queries.GetOpenShiftForTerminal;
using Habbak.ERP.Application.POS.Shifts.Queries.GetShiftById;
using Habbak.ERP.Application.POS.Shifts.Queries.GetShiftsList;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/shifts — screens #1/#3/#14 (05-Module-POS-Shifts.md).</summary>
[ApiController]
[Authorize]
[Screen("POS_SHIFT_CONSOLE", "POS_SHIFTS")]
[MaskFields("Shift")]
[Route("api/v1/pos/shifts")]
public class ShiftsController(ISender mediator) : ControllerBase
{
    public sealed record OpenShiftRequest(long POSTerminalId, IReadOnlyList<DenominationCountInput> OpeningCounts, long? CashierUserId = null, Guid? IdempotencyKey = null);

    public sealed record CloseShiftRequest(string RowVersion, IReadOnlyList<DenominationCountInput> ClosingCounts, Guid? IdempotencyKey = null);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? posTerminalId, [FromQuery] ShiftStatus? status, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetShiftsListQuery(posTerminalId, status), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetShiftByIdQuery(id), cancellationToken));

    [HttpGet("open-for-terminal/{posTerminalId:long}")]
    public async Task<IActionResult> GetOpenForTerminal(long posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOpenShiftForTerminalQuery(posTerminalId), cancellationToken));

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenShiftRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new OpenShiftCommand
        {
            POSTerminalId = request.POSTerminalId,
            CashierUserId = request.CashierUserId,
            OpeningCounts = request.OpeningCounts,
            IdempotencyKey = request.IdempotencyKey
        }, cancellationToken);

        return Ok(new { id });
    }

    [ScreenButton("POS_SHIFT_CONSOLE", "CloseShift")]
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, [FromBody] CloseShiftRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new CloseShiftCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            ClosingCounts = request.ClosingCounts,
            IdempotencyKey = request.IdempotencyKey
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("POS_SHIFTS", "ApproveClose")]
    [HttpPost("{id:long}/approve-close")]
    public async Task<IActionResult> ApproveClose(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveShiftCloseCommand(id), cancellationToken);
        return NoContent();
    }
}
