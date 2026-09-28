using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.POS.Posting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/day-close — "إقفال اليوم": posts a branch's unposted POS invoices for a date.</summary>
[ApiController]
[Authorize]
[Screen("POS_SHIFTS")]
[Route("api/v1/pos/day-close")]
public class POSDayCloseController(ISender mediator) : ControllerBase
{
    public sealed record CloseDayRequest(long BranchId, DateOnly Date);

    [HttpGet]
    public async Task<IActionResult> GetStatus([FromQuery] long branchId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSDayStatusQuery(branchId, date), cancellationToken));

    // Closing the day posts its journal entries — Approve, like posting any document.
    [ScreenButton("POS_SHIFTS", "DayClose")]
    [HttpPost]
    [ScreenAction(Habbak.ERP.Application.Settings.Access.ScreenAction.Approve)]
    public async Task<IActionResult> Close([FromBody] CloseDayRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ClosePOSDayCommand(request.BranchId, request.Date, IdempotencyHeader.Read(this)), cancellationToken));
}
