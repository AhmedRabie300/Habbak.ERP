using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.PaymentMethods.Commands.CreatePaymentMethod;
using Habbak.ERP.Application.Accounting.PaymentMethods.Commands.DeletePaymentMethod;
using Habbak.ERP.Application.Accounting.PaymentMethods.Commands.UpdatePaymentMethod;
using Habbak.ERP.Application.Accounting.PaymentMethods.Queries.GetPaymentMethodsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/payment-methods — dedicated reference list (00-System-Wide-Corrections-02.md, section 2.2).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_PAYMENT_METHODS", LookupReads = true)]
[Route("api/v1/accounting/payment-methods")]
public class PaymentMethodsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePaymentMethodRequest(string? Code, string NameAr, string NameEn);
    public sealed record UpdatePaymentMethodRequest(string NameAr, string NameEn, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPaymentMethodsListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePaymentMethodCommand(request.Code, request.NameAr, request.NameEn), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePaymentMethodRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePaymentMethodCommand(id, request.NameAr, request.NameEn, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePaymentMethodCommand(id), cancellationToken);
        return NoContent();
    }
}
