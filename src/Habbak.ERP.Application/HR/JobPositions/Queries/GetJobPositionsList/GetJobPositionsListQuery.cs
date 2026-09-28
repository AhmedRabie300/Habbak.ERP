using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.JobPositions.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobPositions.Queries.GetJobPositionsList;

/// <summary>Not a paginated GetList screen: a company's job-position catalog is always small.</summary>
public sealed record GetJobPositionsListQuery : IRequest<IReadOnlyList<JobPositionDto>>;

public sealed class GetJobPositionsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetJobPositionsListQuery, IReadOnlyList<JobPositionDto>>
{
    public async Task<IReadOnlyList<JobPositionDto>> Handle(GetJobPositionsListQuery request, CancellationToken cancellationToken)
    {
        return await db.JobPositions
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new JobPositionDto { Id = p.Id, Code = p.Code, NameAr = p.NameAr, NameEn = p.NameEn, IsActive = p.IsActive, OrgUnitId = p.OrgUnitId, DefaultJobGradeId = p.DefaultJobGradeId })
            .ToListAsync(cancellationToken);
    }
}
