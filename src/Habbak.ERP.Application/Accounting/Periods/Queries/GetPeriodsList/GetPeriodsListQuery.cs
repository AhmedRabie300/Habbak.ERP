using Habbak.ERP.Application.Accounting.Periods.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Periods.Queries.GetPeriodsList;

public sealed record GetPeriodsListQuery : IRequest<IReadOnlyList<PeriodListItemDto>>;

public sealed class GetPeriodsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPeriodsListQuery, IReadOnlyList<PeriodListItemDto>>
{
    public async Task<IReadOnlyList<PeriodListItemDto>> Handle(GetPeriodsListQuery request, CancellationToken cancellationToken)
    {
        return await db.AccountingPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.PeriodStart)
            .Select(p => new PeriodListItemDto
            {
                Id = p.Id,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                Status = p.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}
