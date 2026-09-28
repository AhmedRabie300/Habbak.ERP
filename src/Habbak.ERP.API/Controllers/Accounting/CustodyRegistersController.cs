using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Accounting.Custody.Commands.CreateCustodyRegister;
using Habbak.ERP.Application.Accounting.Custody.Commands.CreateCustodySettlement;
using Habbak.ERP.Application.Accounting.Custody.Queries.GetCustodyRegisterById;
using Habbak.ERP.Application.Accounting.Custody.Queries.GetCustodyRegistersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/custody-registers (01-Module-Accounting.md, section 5, screens 8-9).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_CUSTODY")]
[Route("api/v1/accounting/custody-registers")]
public class CustodyRegistersController(ISender mediator) : ControllerBase
{
    public sealed record CreateCustodyRequest(
        long EmployeeId, long? BranchId, decimal Amount, DateOnly IssueDate,
        long TreasuryAccountId, long CustodyReceivableAccountId);

    public sealed record CreateSettlementRequest(
        DateOnly SettlementDate, long CustodyReceivableAccountId, long TreasuryAccountId,
        IReadOnlyList<CustodySettlementLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustodyRegistersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustodyRegisterByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustodyRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCustodyRegisterCommand(
            request.EmployeeId, request.BranchId, request.Amount, request.IssueDate,
            request.TreasuryAccountId, request.CustodyReceivableAccountId), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:long}/settlements")]
    public async Task<IActionResult> CreateSettlement(long id, [FromBody] CreateSettlementRequest request, CancellationToken cancellationToken)
    {
        var settlementId = await mediator.Send(new CreateCustodySettlementCommand(
            id, request.SettlementDate, request.CustodyReceivableAccountId, request.TreasuryAccountId, request.Lines),
            cancellationToken);

        return Ok(new { id = settlementId });
    }
}
