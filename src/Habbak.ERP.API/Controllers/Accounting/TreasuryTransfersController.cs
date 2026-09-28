using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts.Accounting;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.CreateTreasuryTransfer;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.PostTreasuryTransfer;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.UpdateTreasuryTransfer;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Queries.GetTreasuryTransferById;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Queries.GetTreasuryTransfersList;
using Habbak.ERP.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/treasury-transfers (01-Module-Accounting.md, section 5, screen 7).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_TREASURY_TRANSFERS")]
[Route("api/v1/accounting/treasury-transfers")]
public class TreasuryTransfersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTreasuryTransfersListQuery
        {
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTreasuryTransferByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTreasuryTransferRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateTreasuryTransferCommand
        {
            BranchId = request.BranchId,
            FromTreasuryAccountId = request.FromTreasuryAccountId,
            ToTreasuryAccountId = request.ToTreasuryAccountId,
            Amount = request.Amount,
            TransferDate = request.TransferDate,
            Notes = request.Notes
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateTreasuryTransferRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateTreasuryTransferCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            FromTreasuryAccountId = request.FromTreasuryAccountId,
            ToTreasuryAccountId = request.ToTreasuryAccountId,
            Amount = request.Amount,
            TransferDate = request.TransferDate,
            Notes = request.Notes
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new PostTreasuryTransferCommand(id), cancellationToken));
}
