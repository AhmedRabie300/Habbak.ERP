using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Invoices.Commands.CompleteCheckPayment;
using Habbak.ERP.Application.POS.Invoices.Queries.GetPOSInvoiceById;
using Habbak.ERP.Application.POS.Invoices.Queries.GetPOSInvoicesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/invoices — قاعدة 14/15 (05-Module-POS-Shifts.md). إتمام الدفع هو المسار الوحيد
/// لإنشاء POSInvoice — مفيش Create/Update مباشر، الفاتورة نتيجة فعل "دفع" على شيك.</summary>
[ApiController]
[Authorize]
[Screen("POS_TABLE_BOARD", "POS_RETURNS", "POS_SHIFTS")]
[MaskFields("POSPayment")]
[Route("api/v1/pos/invoices")]
public class POSInvoicesController(ISender mediator) : ControllerBase
{
    public sealed record PaymentRequest(long PaymentMethodId, decimal Amount, string? CardTransactionReference, decimal? AmountTendered);
    public sealed record CompletePaymentRequest(long CheckId, decimal TipAmount, IReadOnlyList<PaymentRequest> Payments, Guid? IdempotencyKey = null);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? posTerminalId, [FromQuery] long? shiftId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSInvoicesListQuery(posTerminalId, shiftId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSInvoiceByIdQuery(id), cancellationToken));

    [HttpPost("complete-payment")]
    public async Task<IActionResult> CompletePayment([FromBody] CompletePaymentRequest request, CancellationToken cancellationToken)
    {
        var payments = request.Payments
            .Select(p => new PaymentInput(p.PaymentMethodId, p.Amount, p.CardTransactionReference, p.AmountTendered))
            .ToList();

        var id = await mediator.Send(new CompleteCheckPaymentCommand(request.CheckId, request.TipAmount, payments, request.IdempotencyKey), cancellationToken);
        return Ok(new { id });
    }
}
