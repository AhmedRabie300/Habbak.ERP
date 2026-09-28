using Habbak.ERP.Application.Accounting.Periods.Common;
using Habbak.ERP.Application.Accounting.Periods.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;

namespace Habbak.ERP.Application.Accounting.Periods.Queries.GetPeriodById;

public sealed record GetPeriodByIdQuery(long Id) : IRequest<PeriodDetailDto>;

public sealed class GetPeriodByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPeriodByIdQuery, PeriodDetailDto>
{
    public async Task<PeriodDetailDto> Handle(GetPeriodByIdQuery request, CancellationToken cancellationToken)
    {
        var period = await db.AccountingPeriods.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(AccountingPeriod), request.Id);

        var checklist = period.Status == AccountingPeriodStatus.Open
            ? await PeriodChecklistEvaluator.EvaluateAsync(db, period, cancellationToken)
            : [];

        return new PeriodDetailDto
        {
            Id = period.Id,
            PeriodStart = period.PeriodStart,
            PeriodEnd = period.PeriodEnd,
            Status = period.Status.ToString(),
            ClosedByUserId = period.ClosedByUserId,
            ClosedAtUtc = period.ClosedAtUtc,
            Checklist = checklist,
            CanClose = period.Status == AccountingPeriodStatus.Open && checklist.All(c => c.IsSatisfied)
        };
    }
}
