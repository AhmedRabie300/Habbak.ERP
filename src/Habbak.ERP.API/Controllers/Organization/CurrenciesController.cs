using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Organization.Currencies.Commands.CreateCurrency;
using Habbak.ERP.Application.Organization.Currencies.Commands.DeleteCurrency;
using Habbak.ERP.Application.Organization.Currencies.Commands.UpdateCurrency;
using Habbak.ERP.Application.Organization.Currencies.Queries.GetCurrenciesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Organization;

/// <summary>/organization/currencies — system-wide reference catalog (Currency.cs doc).</summary>
[ApiController]
[Authorize]
[Screen("SETTINGS_CURRENCIES", LookupReads = true)]
[Route("api/v1/organization/currencies")]
public class CurrenciesController(ISender mediator) : ControllerBase
{
    public sealed record CreateCurrencyRequest(string Code, string NameAr, string NameEn, bool IsDefault = false);
    public sealed record UpdateCurrencyRequest(string NameAr, string NameEn, bool IsActive, bool? IsDefault = null);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCurrenciesListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCurrencyRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCurrencyCommand(request.Code, request.NameAr, request.NameEn, request.IsDefault), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCurrencyRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCurrencyCommand(id, request.NameAr, request.NameEn, request.IsActive, request.IsDefault), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCurrencyCommand(id), cancellationToken);
        return NoContent();
    }
}
