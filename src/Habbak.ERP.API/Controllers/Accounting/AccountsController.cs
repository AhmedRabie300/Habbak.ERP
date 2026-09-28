using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.Accounts.Commands.CreateAccount;
using Habbak.ERP.Application.Accounting.Accounts.Commands.DeleteAccount;
using Habbak.ERP.Application.Accounting.Accounts.Commands.UpdateAccount;
using Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountById;
using Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountsList;
using Habbak.ERP.Application.Accounting.Accounts.Queries.GetAccountTree;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>
/// /accounting/accounts — the Chart of Accounts tree (01-Module-Accounting.md, section 5,
/// screen 1) plus the lightweight lookup used by other screens' account pickers.
/// </summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_CHART_OF_ACCOUNTS", LookupReads = true)]
[Route("api/v1/accounting/accounts")]
public class AccountsController(ISender mediator) : ControllerBase
{
    public sealed record CreateAccountRequest(
        string? Code, string NameAr, string NameEn, long? ParentId, AccountType AccountType, AccountNature Nature,
        bool IsPostable, string? CurrencyCode, bool IsSharedAcrossCompanies);

    public sealed record UpdateAccountRequest(
        string RowVersion, string NameAr, string NameEn, AccountType AccountType, AccountNature Nature,
        bool IsPostable, string? CurrencyCode, bool IsActive);

    /// <summary>Lookup list for dropdowns (not the tree) — kept for existing screens' account pickers.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] bool? postableOnly, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetAccountsListQuery(search, postableOnly), cancellationToken));
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAccountTreeQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAccountByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateAccountCommand(
            request.Code, request.NameAr, request.NameEn, request.ParentId, request.AccountType, request.Nature,
            request.IsPostable, request.CurrencyCode, request.IsSharedAcrossCompanies), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAccountCommand(
            id, request.RowVersion, request.NameAr, request.NameEn, request.AccountType, request.Nature,
            request.IsPostable, request.CurrencyCode, request.IsActive), cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteAccountCommand(id), cancellationToken);
        return NoContent();
    }
}
