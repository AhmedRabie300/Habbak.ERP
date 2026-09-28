using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.CancelAccountOpeningBalanceBatch;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.CreateAccountOpeningBalanceBatch;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.PostAccountOpeningBalanceBatch;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Commands.UpdateAccountOpeningBalanceBatch;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Queries.GetAccountOpeningBalanceBatchById;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Queries.GetAccountOpeningBalanceBatchesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/opening-balances — My Remarks/Remarks2.md, bugs 1.4/3.9.</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_OPENING_BALANCES")]
[Route("api/v1/accounting/opening-balances")]
public class AccountOpeningBalancesController(ISender mediator) : ControllerBase
{
    public sealed record CreateAccountOpeningBalanceBatchRequest(
        DateOnly TransactionDate, string? Notes, IReadOnlyList<AccountOpeningBalanceLineInput> Lines);

    public sealed record UpdateAccountOpeningBalanceBatchRequest(
        string RowVersion, DateOnly TransactionDate, string? Notes, IReadOnlyList<AccountOpeningBalanceLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetAccountOpeningBalanceBatchesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAccountOpeningBalanceBatchByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountOpeningBalanceBatchRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateAccountOpeningBalanceBatchCommand
        {
            TransactionDate = request.TransactionDate,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateAccountOpeningBalanceBatchRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAccountOpeningBalanceBatchCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            TransactionDate = request.TransactionDate,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostAccountOpeningBalanceBatchCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelAccountOpeningBalanceBatchCommand(id), cancellationToken);
        return NoContent();
    }
}
