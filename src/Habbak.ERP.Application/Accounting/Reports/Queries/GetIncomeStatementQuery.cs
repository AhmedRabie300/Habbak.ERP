using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>Income Statement (01-Module-Accounting.md, section 8, report 4) — Revenue/Expense movement within the period only.</summary>
public sealed record GetIncomeStatementQuery(DateOnly From, DateOnly To) : IRequest<IncomeStatementDto>;

public sealed class GetIncomeStatementQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetIncomeStatementQuery, IncomeStatementDto>
{
    public async Task<IncomeStatementDto> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
    {
        var lines = await db.JournalEntryLines
            .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.EntryDate >= request.From && l.JournalEntry.EntryDate <= request.To
                && (l.Account.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense))
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

        // Revenue accounts are Credit-natured (Credit - Debit); Expense accounts are Debit-natured (Debit - Credit).
        var revenue = lines
            .Where(l => l.AccountType == AccountType.Revenue)
            .Select(l => new IncomeStatementLineDto { AccountId = l.AccountId, Code = l.Code, Name = l.NameAr, Amount = l.Credit - l.Debit })
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.Code)
            .ToList();

        var expenses = lines
            .Where(l => l.AccountType == AccountType.Expense)
            .Select(l => new IncomeStatementLineDto { AccountId = l.AccountId, Code = l.Code, Name = l.NameAr, Amount = l.Debit - l.Credit })
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.Code)
            .ToList();

        var totalRevenue = revenue.Sum(l => l.Amount);
        var totalExpenses = expenses.Sum(l => l.Amount);

        return new IncomeStatementDto
        {
            Revenue = revenue,
            Expenses = expenses,
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetIncome = totalRevenue - totalExpenses
        };
    }
}
