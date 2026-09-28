using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>Trial Balance (01-Module-Accounting.md, section 8, report 1) — cumulative from inception to AsOf.</summary>
public sealed record GetTrialBalanceQuery(DateOnly AsOf) : IRequest<IReadOnlyList<TrialBalanceLineDto>>;

public sealed class GetTrialBalanceQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTrialBalanceQuery, IReadOnlyList<TrialBalanceLineDto>>
{
    public async Task<IReadOnlyList<TrialBalanceLineDto>> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        return await db.JournalEntryLines
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate <= request.AsOf)
            .GroupBy(l => new { l.AccountId, l.Account.Code, l.Account.NameAr, l.Account.NameEn })
            .Select(g => new TrialBalanceLineDto
            {
                AccountId = g.Key.AccountId,
                Code = g.Key.Code,
                NameAr = g.Key.NameAr,
                NameEn = g.Key.NameEn,
                Debit = g.Sum(l => l.DebitAmount),
                Credit = g.Sum(l => l.CreditAmount)
            })
            .Where(l => l.Debit != 0 || l.Credit != 0)
            .OrderBy(l => l.Code)
            .ToListAsync(cancellationToken);
    }
}
