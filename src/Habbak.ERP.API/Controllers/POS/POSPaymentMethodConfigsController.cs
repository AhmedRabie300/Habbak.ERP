using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.CreatePOSPaymentMethodConfig;
using Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.DeletePOSPaymentMethodConfig;
using Habbak.ERP.Application.POS.PaymentMethodConfigs.Commands.UpdatePOSPaymentMethodConfig;
using Habbak.ERP.Application.POS.PaymentMethodConfigs.Queries.GetPOSPaymentMethodConfigsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/payment-method-configs — screen #12 (05-Module-POS-Shifts.md, section 2.3).</summary>
[ApiController]
[Authorize]
[Screen("POS_PAYMENT_METHOD_CONFIGS", LookupReads = true)]
[Route("api/v1/pos/payment-method-configs")]
public class POSPaymentMethodConfigsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long POSTerminalId, long PaymentMethodId, long LinkedTreasuryAccountId, bool IsEnabled);
    public sealed record UpdateRequest(long LinkedTreasuryAccountId, bool IsEnabled);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSPaymentMethodConfigsListQuery(posTerminalId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePOSPaymentMethodConfigCommand(request.POSTerminalId, request.PaymentMethodId, request.LinkedTreasuryAccountId, request.IsEnabled), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePOSPaymentMethodConfigCommand(id, request.LinkedTreasuryAccountId, request.IsEnabled), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePOSPaymentMethodConfigCommand(id), cancellationToken);
        return NoContent();
    }
}
