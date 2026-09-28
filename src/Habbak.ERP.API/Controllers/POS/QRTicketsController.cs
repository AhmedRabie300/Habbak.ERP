using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.QRTickets.Commands.GenerateQRTicket;
using Habbak.ERP.Application.POS.QRTickets.Commands.RedeemQRTicket;
using Habbak.ERP.Application.POS.QRTickets.Queries.GetQRTicketByKey;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/qr-tickets — قاعدة 18 (05-Module-POS-Shifts.md). Generate يمثّل نقطة إصدار بديلة
/// لحد ما يتبني موديول "استشاري التصنيع" الحقيقي؛ Redeem هو الإجراء اللي شاشة البيع بتناديه بعد
/// مسح الـQR.</summary>
[ApiController]
[Authorize]
[Screen("POS_QR_TICKETS", "POS_TABLE_BOARD", "POS_BLEND_CONSULTATION")]
[Route("api/v1/pos/qr-tickets")]
public class QRTicketsController(ISender mediator) : ControllerBase
{
    public sealed record ItemRequest(long ItemId, decimal Quantity, decimal UnitPrice);
    public sealed record GenerateRequest(IReadOnlyList<ItemRequest> Items);
    public sealed record RedeemRequest(long CheckId, Guid IdempotencyKey);

    [HttpGet("{idempotencyKey:guid}")]
    public async Task<IActionResult> GetByKey(Guid idempotencyKey, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetQRTicketByKeyQuery(idempotencyKey), cancellationToken));

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(i => new QRTicketItemInput(i.ItemId, i.Quantity, i.UnitPrice)).ToList();
        var result = await mediator.Send(new GenerateQRTicketCommand(items), cancellationToken);
        return Ok(new { id = result.Id, idempotencyKey = result.IdempotencyKey });
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RedeemQRTicketCommand(request.CheckId, request.IdempotencyKey), cancellationToken);
        return NoContent();
    }
}
