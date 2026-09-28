using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>
/// Balance Sheet (01-Module-Accounting.md, section 8, report 5) — cumulative from inception to
/// AsOf. Revenue/Expense accounts are never automatically closed into Equity by a period-close
/// entry in this build, so accumulated net income to date is added to Equity as its own line
/// ("أرباح/خسائر الفترة الحالية") to keep Assets = Liabilities + Equity true at any point.
/// </summary>
public sealed record GetBalanceSheetQuery(DateOnly AsOf) : IRequest<BalanceSheetDto>;

public sealed class GetBalanceSheetQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var lines = await db.JournalEntryLines
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted && l.JournalEntry.EntryDate <= request.AsOf)
            .GroupBy(l => new { l.AccountId, l.Account.Code, l.Account.NameAr, l.Account.AccountType })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Code,
                g.Key.NameAr,
                g.Key.AccountType,
                Debit = g.Sum(l => l.DebitAmount),
                Credit = g.Sum(l => l.CreditAmount)
            })
            .ToListAsync(cancellationToken);

        var assets = lines
            .Where(l => l.AccountType == AccountType.Asset)
            .Select(l => new IncomeStatementLineDto { AccountId = l.AccountId, Code = l.Code, Name = l.NameAr, Amount = l.Debit - l.Credit })
            .Where(l => l.Amount != 0).OrderBy(l => l.Code).ToList();

        var liabilities = lines
            .Where(l => l.AccountType == AccountType.Liability)
            .Select(l => new IncomeStatementLineDto { AccountId = l.AccountId, Code = l.Code, Name = l.NameAr, Amount = l.Credit - l.Debit })
            .Where(l => l.Amount != 0).OrderBy(l => l.Code).ToList();

        var equity = lines
            .Where(l => l.AccountType == AccountType.Equity)
            .Select(l => new IncomeStatementLineDto { AccountId = l.AccountId, Code = l.Code, Name = l.NameAr, Amount = l.Credit - l.Debit })
            .Where(l => l.Amount != 0).OrderBy(l => l.Code).ToList();

        var revenueTotal = lines.Where(l => l.AccountType == AccountType.Revenue).Sum(l => l.Credit - l.Debit);
        var expenseTotal = lines.Where(l => l.AccountType == AccountType.Expense).Sum(l => l.Debit - l.Credit);
        var netIncomeToDate = revenueTotal - expenseTotal;

        if (netIncomeToDate != 0)
        {
            equity = [.. equity, new IncomeStatementLineDto { AccountId = 0, Code = "-", Name = "أرباح/خسائر الفترة الحالية", Amount = netIncomeToDate }];
        }

        return new BalanceSheetDto
        {
            Assets = assets,
            Liabilities = liabilities,
            Equity = equity,
            TotalAssets = assets.Sum(l => l.Amount),
            TotalLiabilities = liabilities.Sum(l => l.Amount),
            TotalEquity = equity.Sum(l => l.Amount)
        };
    }
}
