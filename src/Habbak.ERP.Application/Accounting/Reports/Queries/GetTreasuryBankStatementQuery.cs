using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>My Remarks/Remarks2.md, remark 3.10, report 6 — "تقرير الخزائن والبنوك": unlike
/// GetTreasuryPositionQuery's single as-of balance, this returns every treasury/bank account's
/// opening balance, period movements and closing balance — the same computation as
/// GetAccountStatementQuery, batched across every treasury/bank account at once (rather than one
/// query per account) so a busy company with many treasuries stays cheap to run.</summary>
public sealed record GetTreasuryBankStatementQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<TreasuryAccountStatementDto>>;

public sealed class GetTreasuryBankStatementQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTreasuryBankStatementQuery, IReadOnlyList<TreasuryAccountStatementDto>>
{
    public async Task<IReadOnlyList<TreasuryAccountStatementDto>> Handle(GetTreasuryBankStatementQuery request, CancellationToken cancellationToken)
    {
        var treasuryAccountIds = await TreasuryAccountsLookup.GetTreasuryAccountIdsAsync(db, cancellationToken);
        if (treasuryAccountIds.Count == 0)
        {
            return [];
        }

        var accounts = await db.Accounts
            .Where(a => treasuryAccountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code, a.NameAr, a.NameEn })
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var openingBalances = await db.JournalEntryLines
            .Where(l => treasuryAccountIds.Contains(l.AccountId)
                && l.JournalEntry.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate < request.From)
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(l => l.DebitAmount - l.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance, cancellationToken);

        var movements = await db.JournalEntryLines
            .Where(l => treasuryAccountIds.Contains(l.AccountId)
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= request.From && l.JournalEntry.EntryDate <= request.To)
            .OrderBy(l => l.JournalEntry.EntryDate).ThenBy(l => l.JournalEntry.Id).ThenBy(l => l.LineNumber)
            .Select(l => new
            {
                l.AccountId,
                l.JournalEntry.EntryDate,
                l.JournalEntry.EntryNumber,
                Description = l.Description ?? l.JournalEntry.Description,
                l.DebitAmount,
                l.CreditAmount
            })
            .ToListAsync(cancellationToken);

        var movementsByAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<TreasuryAccountStatementDto>();
        foreach (var account in accounts)
        {
            var runningBalance = openingBalances.GetValueOrDefault(account.Id);
            var lines = new List<AccountStatementLineDto>();
            foreach (var m in movementsByAccount.GetValueOrDefault(account.Id, []))
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

            result.Add(new TreasuryAccountStatementDto
            {
                AccountId = account.Id,
                Code = account.Code,
                NameAr = account.NameAr,
                NameEn = account.NameEn,
                OpeningBalance = openingBalances.GetValueOrDefault(account.Id),
                ClosingBalance = runningBalance,
                Lines = lines
            });
        }

        return result;
    }
}
