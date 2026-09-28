using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.BankReconciliations.Commands.AddBankReconciliationLine;
using Habbak.ERP.Application.Accounting.BankReconciliations.Commands.CompleteBankReconciliation;
using Habbak.ERP.Application.Accounting.BankReconciliations.Commands.CreateBankReconciliationRun;
using Habbak.ERP.Application.Accounting.BankReconciliations.Queries.GetBankReconciliationById;
using Habbak.ERP.Application.Accounting.BankReconciliations.Queries.GetBankReconciliationsList;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/bank-reconciliations (01-Module-Accounting.md, section 5, screen 11).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_BANK_RECONCILIATIONS")]
[Route("api/v1/accounting/bank-reconciliations")]
public class BankReconciliationsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRunRequest(long BankAccountId, DateOnly PeriodFrom, DateOnly PeriodTo);

    public sealed record AddLineRequest(
        SystemTransactionType? SystemTransactionType, long? SystemTransactionId,
        long? BankStatementLineId, decimal MatchedAmount, bool IsAutoMatched);

    public sealed record CompleteRequest(decimal AdjustmentAmount, long? AdjustmentAccountId);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBankReconciliationsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBankReconciliationByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRunRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new CreateBankReconciliationRunCommand(request.BankAccountId, request.PeriodFrom, request.PeriodTo), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:long}/lines")]
    public async Task<IActionResult> AddLine(long id, [FromBody] AddLineRequest request, CancellationToken cancellationToken)
    {
        var lineId = await mediator.Send(new AddBankReconciliationLineCommand(
            id, request.SystemTransactionType, request.SystemTransactionId, request.BankStatementLineId,
            request.MatchedAmount, request.IsAutoMatched), cancellationToken);

        return Ok(new { id = lineId });
    }

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteBankReconciliationCommand(id, request.AdjustmentAmount, request.AdjustmentAccountId), cancellationToken);
        return NoContent();
    }
}
