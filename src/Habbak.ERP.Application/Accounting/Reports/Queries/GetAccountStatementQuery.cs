using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>
/// Shared by "دفتر الأستاذ العام" (report 2, any account) and "كشف حساب خزينة" (report 3,
/// picked as a treasury/bank account by the caller) — both are the same computation, just
/// scoped to a different account by the frontend's dropdown (01-Module-Accounting.md, section 8).
/// </summary>
public sealed record GetAccountStatementQuery(long AccountId, DateOnly From, DateOnly To) : IRequest<AccountStatementDto>;

public sealed class GetAccountStatementQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountStatementQuery, AccountStatementDto>
{
    public async Task<AccountStatementDto> Handle(GetAccountStatementQuery request, CancellationToken cancellationToken)
    {
        var openingBalance = await db.JournalEntryLines
            .Where(l => l.AccountId == request.AccountId
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate < request.From)
            .SumAsync(l => l.DebitAmount - l.CreditAmount, cancellationToken);

        var movements = await db.JournalEntryLines
            .Where(l => l.AccountId == request.AccountId
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= request.From && l.JournalEntry.EntryDate <= request.To)
            .OrderBy(l => l.JournalEntry.EntryDate).ThenBy(l => l.JournalEntry.Id).ThenBy(l => l.LineNumber)
            .Select(l => new
            {
                l.JournalEntry.EntryDate,
                l.JournalEntry.EntryNumber,
                Description = l.Description ?? l.JournalEntry.Description,
                l.DebitAmount,
                l.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var runningBalance = openingBalance;
        var lines = new List<AccountStatementLineDto>();
        foreach (var m in movements)
        {
            runningBalance += m.DebitAmount - m.CreditAmount;
            lines.Add(new AccountStatementLineDto
            {
                Date = m.EntryDate,
                EntryNumber = m.EntryNumber,
                Description = m.Description,
                Debit = m.DebitAmount,
                Credit = m.CreditAmount,
                RunningBalance = runningBalance
            });
        }

        return new AccountStatementDto
        {
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            Lines = lines
        };
    }
}
