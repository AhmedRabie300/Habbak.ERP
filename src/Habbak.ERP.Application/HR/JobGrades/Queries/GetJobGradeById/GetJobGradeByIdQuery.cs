using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.JobGrades.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobGrades.Queries.GetJobGradeById;

public sealed record GetJobGradeByIdQuery(long Id) : IRequest<JobGradeDto>;

public sealed class GetJobGradeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetJobGradeByIdQuery, JobGradeDto>
{
    public async Task<JobGradeDto> Handle(GetJobGradeByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.JobGrades
            .AsNoTracking()
            .Where(g => g.Id == request.Id)
            .Select(g => new JobGradeDto { Id = g.Id, Code = g.Code, NameAr = g.NameAr, NameEn = g.NameEn, IsActive = g.IsActive, Level = g.Level, MinSalary = g.MinSalary, MaxSalary = g.MaxSalary })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(JobGrade), request.Id);
    }
}
