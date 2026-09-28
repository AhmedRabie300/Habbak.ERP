using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>Cost Center Statement (01-Module-Accounting.md, section 8, report 11).</summary>
public sealed record GetCostCenterStatementQuery(long DimensionValueId, DateOnly From, DateOnly To)
    : IRequest<AccountStatementDto>;

public sealed class GetCostCenterStatementQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCostCenterStatementQuery, AccountStatementDto>
{
    public async Task<AccountStatementDto> Handle(GetCostCenterStatementQuery request, CancellationToken cancellationToken)
    {
        var movements = await db.JournalEntryLineDimensionValues
            .Where(v => v.CostCenterDimensionValueId == request.DimensionValueId
                && v.JournalEntryLine.JournalEntry.Status == JournalEntryStatus.Posted
                && v.JournalEntryLine.JournalEntry.EntryDate >= request.From
                && v.JournalEntryLine.JournalEntry.EntryDate <= request.To)
            .OrderBy(v => v.JournalEntryLine.JournalEntry.EntryDate).ThenBy(v => v.JournalEntryLine.JournalEntry.Id)
            .Select(v => new
            {
                v.JournalEntryLine.JournalEntry.EntryDate,
                v.JournalEntryLine.JournalEntry.EntryNumber,
                Description = v.JournalEntryLine.Description ?? v.JournalEntryLine.JournalEntry.Description,
                v.JournalEntryLine.DebitAmount,
                v.JournalEntryLine.CreditAmount
            })
            .ToListAsync(cancellationToken);

        decimal running = 0;
        var lines = new List<AccountStatementLineDto>();
        foreach (var m in movements)
        {
            running += m.DebitAmount - m.CreditAmount;
            lines.Add(new AccountStatementLineDto
            {
                Date = m.EntryDate,
                EntryNumber = m.EntryNumber,
                Description = m.Description,
                Debit = m.DebitAmount,
                Credit = m.CreditAmount,
                RunningBalance = running
            });
        }

        return new AccountStatementDto { OpeningBalance = 0, ClosingBalance = running, Lines = lines };
    }
}
