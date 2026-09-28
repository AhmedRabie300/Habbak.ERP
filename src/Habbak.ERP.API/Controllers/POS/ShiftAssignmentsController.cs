using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.ShiftAssignments.Commands.CreateShiftAssignment;
using Habbak.ERP.Application.POS.ShiftAssignments.Commands.DeleteShiftAssignment;
using Habbak.ERP.Application.POS.ShiftAssignments.Queries.GetShiftAssignmentsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/shift-assignments — screen مبدئي (05-Module-POS-Shifts.md), قاعدة 31 (الكاشير
/// المعيَّن على الجهاز في يوم بعينه).</summary>
[ApiController]
[Authorize]
[Screen("POS_SHIFT_ASSIGNMENTS")]
[Route("api/v1/pos/shift-assignments")]
public class ShiftAssignmentsController(ISender mediator) : ControllerBase
{
    public sealed record CreateShiftAssignmentRequest(long POSTerminalId, long UserId, DateOnly AssignedDate);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetShiftAssignmentsListQuery(posTerminalId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShiftAssignmentRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateShiftAssignmentCommand(request.POSTerminalId, request.UserId, request.AssignedDate), cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteShiftAssignmentCommand(id), cancellationToken);
        return NoContent();
    }
}
