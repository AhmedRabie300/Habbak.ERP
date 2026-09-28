using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>
/// SIMPLIFIED Cash Flow Statement (01-Module-Accounting.md, section 8, report 6): a direct net
/// movement across the caller-selected treasury/bank accounts for the period, not a full
/// indirect-method statement classified into operating/investing/financing activities — the
/// Account entity has no "is treasury/cash account" sub-type to classify that automatically
/// (01-Module-Accounting.md, section 2.1 has no such field), so the caller must say which
/// accounts to include (matching how every other screen in this module picks a treasury
/// account: a plain dropdown over all accounts, not a flagged subset).
/// </summary>
public sealed record GetCashFlowQuery(IReadOnlyList<long> AccountIds, DateOnly From, DateOnly To) : IRequest<CashFlowDto>;

public sealed class GetCashFlowQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCashFlowQuery, CashFlowDto>
{
    public async Task<CashFlowDto> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
    {
        var openingBalance = await db.JournalEntryLines
            .Where(l => request.AccountIds.Contains(l.AccountId)
                && l.JournalEntry.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate < request.From)
            .SumAsync(l => l.DebitAmount - l.CreditAmount, cancellationToken);

        var periodLines = await db.JournalEntryLines
            .Where(l => request.AccountIds.Contains(l.AccountId)
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= request.From && l.JournalEntry.EntryDate <= request.To)
            .Select(l => new { l.DebitAmount, l.CreditAmount })
            .ToListAsync(cancellationToken);

        var totalDebit = periodLines.Sum(l => l.DebitAmount);
        var totalCredit = periodLines.Sum(l => l.CreditAmount);

        return new CashFlowDto
        {
            OpeningBalance = openingBalance,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = openingBalance + totalDebit - totalCredit
        };
    }
}
