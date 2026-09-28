using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>Treasury &amp; Bank Position (01-Module-Accounting.md, section 8, report 12) — a
/// balance-only snapshot as of one date; see GetTreasuryBankStatementQuery for the movements
/// version. Treasury accounts are TreasuryAccountsLookup's derived set.</summary>
public sealed record GetTreasuryPositionQuery(DateOnly AsOf) : IRequest<IReadOnlyList<TreasuryPositionLineDto>>;

public sealed class GetTreasuryPositionQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTreasuryPositionQuery, IReadOnlyList<TreasuryPositionLineDto>>
{
    public async Task<IReadOnlyList<TreasuryPositionLineDto>> Handle(GetTreasuryPositionQuery request, CancellationToken cancellationToken)
    {
        var treasuryAccountIds = await TreasuryAccountsLookup.GetTreasuryAccountIdsAsync(db, cancellationToken);

        if (treasuryAccountIds.Count == 0)
        {
            return [];
        }

        var accounts = await db.Accounts
            .Where(a => treasuryAccountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code, a.NameAr, a.NameEn })
            .ToListAsync(cancellationToken);

        var balances = await db.JournalEntryLines
            .Where(l => treasuryAccountIds.Contains(l.AccountId)
                && l.JournalEntry.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate <= request.AsOf)
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(l => l.DebitAmount - l.CreditAmount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance, cancellationToken);

        return accounts
            .Select(a => new TreasuryPositionLineDto
            {
                AccountId = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                Balance = balances.GetValueOrDefault(a.Id)
            })
            .OrderBy(l => l.Code)
            .ToList();
    }
}
