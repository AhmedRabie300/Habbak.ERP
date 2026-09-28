using Habbak.ERP.Application.Accounting.CashReconciliations.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.CashReconciliations.Queries.GetCashReconciliationsList;

public sealed record GetCashReconciliationsListQuery : IRequest<IReadOnlyList<CashReconciliationListItemDto>>;

public sealed class GetCashReconciliationsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCashReconciliationsListQuery, IReadOnlyList<CashReconciliationListItemDto>>
{
    public async Task<IReadOnlyList<CashReconciliationListItemDto>> Handle(GetCashReconciliationsListQuery request, CancellationToken cancellationToken)
    {
        return await db.CashReconciliations
            .AsNoTracking()
            .OrderByDescending(c => c.ReconciliationDate)
            .Select(c => new CashReconciliationListItemDto
            {
                Id = c.Id,
                TreasuryAccountId = c.TreasuryAccountId,
                ReconciliationDate = c.ReconciliationDate,
                ExpectedBalance = c.ExpectedBalance,
                ActualBalance = c.ActualBalance,
                DifferenceAmount = c.DifferenceAmount,
                IsApproved = c.ApprovedAtUtc != null
            })
            .ToListAsync(cancellationToken);
    }
}
