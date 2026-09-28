using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.CashReconciliations.Commands.ApproveCashReconciliation;
using Habbak.ERP.Application.Accounting.CashReconciliations.Commands.CreateCashReconciliation;
using Habbak.ERP.Application.Accounting.CashReconciliations.Queries.GetCashReconciliationById;
using Habbak.ERP.Application.Accounting.CashReconciliations.Queries.GetCashReconciliationsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/cash-reconciliations (01-Module-Accounting.md, section 5, screen 10).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_CASH_RECONCILIATIONS")]
[Route("api/v1/accounting/cash-reconciliations")]
public class CashReconciliationsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(
        long TreasuryAccountId, long? BranchId, DateOnly ReconciliationDate, decimal ActualBalance,
        string? DifferenceReason, IReadOnlyList<DenominationInput>? Denominations);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCashReconciliationsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCashReconciliationByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCashReconciliationCommand(
            request.TreasuryAccountId, request.BranchId, request.ReconciliationDate, request.ActualBalance,
            request.DifferenceReason, request.Denominations), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveCashReconciliationCommand(id), cancellationToken);
        return NoContent();
    }
}
