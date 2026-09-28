using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>My Remarks/Remarks2.md, remark 3.10, report 2 — "دفتر اليومية": every journal entry
/// (any status, unlike Trial Balance which is Posted-only) within a period, each with its own
/// line breakdown, since a journal book is meant to be read entry-by-entry rather than as
/// per-account totals (that's Trial Balance's/General Ledger's job).</summary>
public sealed record GetJournalBookQuery(DateOnly From, DateOnly To, JournalEntryStatus? Status) : IRequest<IReadOnlyList<JournalBookEntryDto>>;

public sealed class GetJournalBookQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetJournalBookQuery, IReadOnlyList<JournalBookEntryDto>>
{
    public async Task<IReadOnlyList<JournalBookEntryDto>> Handle(GetJournalBookQuery request, CancellationToken cancellationToken)
    {
        var entries = await db.JournalEntries
            .AsNoTracking()
            .Where(e => e.EntryDate >= request.From && e.EntryDate <= request.To)
            .Where(e => request.Status == null || e.Status == request.Status)
            .Include(e => e.Lines).ThenInclude(l => l.Account)
            .OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber)
            .ToListAsync(cancellationToken);

        return entries
            .Select(e => new JournalBookEntryDto
            {
                Id = e.Id,
                EntryNumber = e.EntryNumber,
                EntryDate = e.EntryDate,
                Description = e.Description,
                Status = e.Status.ToString(),
                TotalDebit = e.TotalDebit,
                TotalCredit = e.TotalCredit,
                Lines = e.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new JournalBookLineDto
                    {
                        AccountId = l.AccountId,
                        AccountCode = l.Account.Code,
                        AccountName = l.Account.NameAr,
                        Debit = l.DebitAmount,
                        Credit = l.CreditAmount
                    })
                    .ToList()
            })
            .ToList();
    }
}
