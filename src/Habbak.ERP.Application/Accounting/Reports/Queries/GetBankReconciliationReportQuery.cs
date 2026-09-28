using Habbak.ERP.Application.Accounting.Reports.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>My Remarks/Remarks2.md, remark 3.10, report 8 — "تقرير المطابقات البنكية": every
/// matched line (movement) of every reconciliation run for one bank account whose period overlaps
/// [From, To], flattened into one list — unlike the bank-reconciliations List/Edit screens, which
/// only ever show one run's lines at a time.</summary>
public sealed record GetBankReconciliationReportQuery(long BankAccountId, DateOnly From, DateOnly To) : IRequest<IReadOnlyList<BankReconciliationReportLineDto>>;

public sealed class GetBankReconciliationReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBankReconciliationReportQuery, IReadOnlyList<BankReconciliationReportLineDto>>
{
    public async Task<IReadOnlyList<BankReconciliationReportLineDto>> Handle(GetBankReconciliationReportQuery request, CancellationToken cancellationToken)
    {
        var runs = await db.BankReconciliationRuns
            .AsNoTracking()
            .Where(r => r.BankAccountId == request.BankAccountId
                && r.PeriodFrom <= request.To && r.PeriodTo >= request.From)
            .Include(r => r.Lines)
            .OrderBy(r => r.PeriodFrom)
            .ToListAsync(cancellationToken);

        return runs
            .SelectMany(r => r.Lines.Select(l => new BankReconciliationReportLineDto
            {
                RunId = r.Id,
                PeriodFrom = r.PeriodFrom,
                PeriodTo = r.PeriodTo,
                RunStatus = r.Status.ToString(),
                SystemTransactionType = l.SystemTransactionType?.ToString(),
                SystemTransactionId = l.SystemTransactionId,
                BankStatementLineId = l.BankStatementLineId,
                MatchedAmount = l.MatchedAmount,
                IsAutoMatched = l.IsAutoMatched
            }))
            .ToList();
    }
}
