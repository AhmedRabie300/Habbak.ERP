using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.Periods.Commands.ClosePeriod;
using Habbak.ERP.Application.Accounting.Periods.Commands.CreatePeriod;
using Habbak.ERP.Application.Accounting.Periods.Commands.ReopenPeriod;
using Habbak.ERP.Application.Accounting.Periods.Queries.GetPeriodById;
using Habbak.ERP.Application.Accounting.Periods.Queries.GetPeriodsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/accounting-periods (01-Module-Accounting.md, section 5, screen 12).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_PERIODS", LookupReads = true)]
[Route("api/v1/accounting/accounting-periods")]
public class AccountingPeriodsController(ISender mediator) : ControllerBase
{
    public sealed record CreatePeriodRequest(DateOnly PeriodStart, DateOnly PeriodEnd);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPeriodsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPeriodByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePeriodRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePeriodCommand(request.PeriodStart, request.PeriodEnd), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ClosePeriodCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reopen")]
    public async Task<IActionResult> Reopen(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReopenPeriodCommand(id), cancellationToken);
        return NoContent();
    }
}
