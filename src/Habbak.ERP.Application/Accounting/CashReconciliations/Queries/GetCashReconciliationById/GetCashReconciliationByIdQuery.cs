using Habbak.ERP.Application.Accounting.CashReconciliations.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.CashReconciliations.Queries.GetCashReconciliationById;

public sealed record GetCashReconciliationByIdQuery(long Id) : IRequest<CashReconciliationDetailDto>;

public sealed class GetCashReconciliationByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCashReconciliationByIdQuery, CashReconciliationDetailDto>
{
    public async Task<CashReconciliationDetailDto> Handle(GetCashReconciliationByIdQuery request, CancellationToken cancellationToken)
    {
        var reconciliation = await db.CashReconciliations
            .AsNoTracking()
            .Include(c => c.Denominations)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CashReconciliation), request.Id);

        return new CashReconciliationDetailDto
        {
            Id = reconciliation.Id,
            TreasuryAccountId = reconciliation.TreasuryAccountId,
            ReconciliationDate = reconciliation.ReconciliationDate,
            ExpectedBalance = reconciliation.ExpectedBalance,
            ActualBalance = reconciliation.ActualBalance,
            DifferenceAmount = reconciliation.DifferenceAmount,
            IsApproved = reconciliation.ApprovedAtUtc != null,
            DifferenceReason = reconciliation.DifferenceReason,
            ApprovedByUserId = reconciliation.ApprovedByUserId,
            ApprovedAtUtc = reconciliation.ApprovedAtUtc,
            Denominations = reconciliation.Denominations
                .Select(d => new DenominationDto { DenominationValue = d.DenominationValue, Count = d.Count })
                .ToList()
        };
    }
}
