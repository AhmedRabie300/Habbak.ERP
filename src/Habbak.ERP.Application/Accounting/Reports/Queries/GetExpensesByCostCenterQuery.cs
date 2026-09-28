using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>Expenses by Cost Center (01-Module-Accounting.md, section 8, report 13).</summary>
public sealed record GetExpensesByCostCenterQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<CostCenterExpenseLineDto>>;

public sealed class GetExpensesByCostCenterQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetExpensesByCostCenterQuery, IReadOnlyList<CostCenterExpenseLineDto>>
{
    public async Task<IReadOnlyList<CostCenterExpenseLineDto>> Handle(GetExpensesByCostCenterQuery request, CancellationToken cancellationToken)
    {
        var costCenterDimensionId = await db.CostCenterDimensions
            .Where(d => d.Code == CostCenterDimensionCodes.CostCenter)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (costCenterDimensionId is null)
        {
            return [];
        }

        return await db.JournalEntryLineDimensionValues
            .Where(v => v.CostCenterDimensionId == costCenterDimensionId
                && v.JournalEntryLine.Account.AccountType == AccountType.Expense
                && v.JournalEntryLine.JournalEntry.Status == JournalEntryStatus.Posted
                && v.JournalEntryLine.JournalEntry.EntryDate >= request.From
                && v.JournalEntryLine.JournalEntry.EntryDate <= request.To)
            .GroupBy(v => new { v.CostCenterDimensionValueId, v.CostCenterDimensionValue.NameAr, v.CostCenterDimensionValue.NameEn })
            .Select(g => new CostCenterExpenseLineDto
            {
                DimensionValueId = g.Key.CostCenterDimensionValueId,
                NameAr = g.Key.NameAr,
                NameEn = g.Key.NameEn,
                Amount = g.Sum(v => v.JournalEntryLine.DebitAmount - v.JournalEntryLine.CreditAmount)
            })
            .OrderByDescending(l => l.Amount)
            .ToListAsync(cancellationToken);
    }
}
