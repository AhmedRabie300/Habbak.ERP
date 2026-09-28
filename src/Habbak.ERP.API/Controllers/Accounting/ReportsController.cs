using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.Reports.Queries;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/reports (01-Module-Accounting.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_REPORTS")]
[Route("api/v1/accounting/reports")]
public class ReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] DateOnly asOf, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTrialBalanceQuery(asOf), cancellationToken));

    [HttpGet("account-statement")]
    public async Task<IActionResult> AccountStatement(
        [FromQuery] long accountId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAccountStatementQuery(accountId, from, to), cancellationToken));

    [HttpGet("income-statement")]
    public async Task<IActionResult> IncomeStatement([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetIncomeStatementQuery(from, to), cancellationToken));

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateOnly asOf, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBalanceSheetQuery(asOf), cancellationToken));

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(
        [FromQuery] long[] accountIds, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCashFlowQuery(accountIds, from, to), cancellationToken));

    [HttpGet("treasury-position")]
    public async Task<IActionResult> TreasuryPosition([FromQuery] DateOnly asOf, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTreasuryPositionQuery(asOf), cancellationToken));

    [HttpGet("cost-center-statement")]
    public async Task<IActionResult> CostCenterStatement(
        [FromQuery] long dimensionValueId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCostCenterStatementQuery(dimensionValueId, from, to), cancellationToken));

    [HttpGet("expenses-by-cost-center")]
    public async Task<IActionResult> ExpensesByCostCenter([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetExpensesByCostCenterQuery(from, to), cancellationToken));

    [HttpGet("journal-book")]
    public async Task<IActionResult> JournalBook(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] JournalEntryStatus? status, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetJournalBookQuery(from, to, status), cancellationToken));

    [HttpGet("treasury-bank-statement")]
    public async Task<IActionResult> TreasuryBankStatement([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTreasuryBankStatementQuery(from, to), cancellationToken));

    [HttpGet("custody-report")]
    public async Task<IActionResult> CustodyReport(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustodyReportQuery(), cancellationToken));

    [HttpGet("bank-reconciliation-report")]
    public async Task<IActionResult> BankReconciliationReport(
        [FromQuery] long bankAccountId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBankReconciliationReportQuery(bankAccountId, from, to), cancellationToken));
}
