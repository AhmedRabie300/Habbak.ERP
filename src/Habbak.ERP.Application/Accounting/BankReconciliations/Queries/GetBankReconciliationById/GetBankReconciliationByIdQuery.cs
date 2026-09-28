using Habbak.ERP.Application.Accounting.BankReconciliations.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.BankReconciliations.Queries.GetBankReconciliationById;

public sealed record GetBankReconciliationByIdQuery(long Id) : IRequest<BankReconciliationDetailDto>;

public sealed class GetBankReconciliationByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBankReconciliationByIdQuery, BankReconciliationDetailDto>
{
    public async Task<BankReconciliationDetailDto> Handle(GetBankReconciliationByIdQuery request, CancellationToken cancellationToken)
    {
        var run = await db.BankReconciliationRuns
            .AsNoTracking()
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BankReconciliationRun), request.Id);

        return new BankReconciliationDetailDto
        {
            Id = run.Id,
            BankAccountId = run.BankAccountId,
            PeriodFrom = run.PeriodFrom,
            PeriodTo = run.PeriodTo,
            Status = run.Status.ToString(),
            AdjustmentJournalEntryId = run.AdjustmentJournalEntryId,
            Lines = run.Lines.Select(l => new BankReconciliationLineDto
            {
                Id = l.Id,
                SystemTransactionType = l.SystemTransactionType?.ToString(),
                SystemTransactionId = l.SystemTransactionId,
                BankStatementLineId = l.BankStatementLineId,
                MatchedAmount = l.MatchedAmount,
                IsAutoMatched = l.IsAutoMatched
            }).ToList()
        };
    }
}
