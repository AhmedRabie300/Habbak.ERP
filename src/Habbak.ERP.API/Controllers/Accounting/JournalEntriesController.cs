using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts.Accounting;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.CreateManualJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.DeleteJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.PostJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.ReverseJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.UpdateManualJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Queries.GetJournalEntriesList;
using Habbak.ERP.Application.Accounting.JournalEntries.Queries.GetJournalEntryById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>
/// /accounting/journal-entries (00-Frontend-Specs.md, section 5 — the standard List/Search/Edit
/// pattern, plus the module's own status actions: post/reverse).
/// </summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_JOURNAL_ENTRIES", "ACCOUNTING_POSTING_REPORTS")]
[Route("api/v1/accounting/journal-entries")]
public class JournalEntriesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetJournalEntriesListQuery query, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetJournalEntryByIdQuery(id), cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJournalEntryRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateManualJournalEntryCommand
        {
            BranchId = request.BranchId,
            EntryDate = request.EntryDate,
            Description = request.Description,
            Lines = request.Lines
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateJournalEntryRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateManualJournalEntryCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            EntryDate = request.EntryDate,
            Description = request.Description,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new PostJournalEntryCommand(id), cancellationToken));
    }

    /// <summary>"إنشاء قيد عكسي" — returns the new reversal entry's Id (00-Project-Overview.md rule 16).</summary>
    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, CancellationToken cancellationToken)
    {
        var newEntryId = await mediator.Send(new ReverseJournalEntryCommand(id), cancellationToken);
        return Ok(new { id = newEntryId });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteJournalEntryCommand(id), cancellationToken);
        return NoContent();
    }
}
