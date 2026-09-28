using Habbak.ERP.Application.Accounting.BankReconciliations.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.BankReconciliations.Queries.GetBankReconciliationsList;

public sealed record GetBankReconciliationsListQuery : IRequest<IReadOnlyList<BankReconciliationListItemDto>>;

public sealed class GetBankReconciliationsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBankReconciliationsListQuery, IReadOnlyList<BankReconciliationListItemDto>>
{
    public async Task<IReadOnlyList<BankReconciliationListItemDto>> Handle(GetBankReconciliationsListQuery request, CancellationToken cancellationToken)
    {
        return await db.BankReconciliationRuns
            .AsNoTracking()
            .OrderByDescending(r => r.PeriodTo)
            .Select(r => new BankReconciliationListItemDto
            {
                Id = r.Id,
                BankAccountId = r.BankAccountId,
                PeriodFrom = r.PeriodFrom,
                PeriodTo = r.PeriodTo,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}
