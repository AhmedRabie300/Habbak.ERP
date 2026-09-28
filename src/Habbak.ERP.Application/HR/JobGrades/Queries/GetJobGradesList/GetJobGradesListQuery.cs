using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.JobGrades.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobGrades.Queries.GetJobGradesList;

/// <summary>Not a paginated GetList screen: a company's job-grade scale is always small.</summary>
public sealed record GetJobGradesListQuery : IRequest<IReadOnlyList<JobGradeDto>>;

public sealed class GetJobGradesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetJobGradesListQuery, IReadOnlyList<JobGradeDto>>
{
    public async Task<IReadOnlyList<JobGradeDto>> Handle(GetJobGradesListQuery request, CancellationToken cancellationToken)
    {
        return await db.JobGrades
            .AsNoTracking()
            .OrderBy(g => g.Level)
            .Select(g => new JobGradeDto { Id = g.Id, Code = g.Code, NameAr = g.NameAr, NameEn = g.NameEn, IsActive = g.IsActive, Level = g.Level, MinSalary = g.MinSalary, MaxSalary = g.MaxSalary })
            .ToListAsync(cancellationToken);
    }
}
