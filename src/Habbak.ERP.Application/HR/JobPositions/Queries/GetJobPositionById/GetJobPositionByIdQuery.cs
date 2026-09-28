using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.JobPositions.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.JobPositions.Queries.GetJobPositionById;

public sealed record GetJobPositionByIdQuery(long Id) : IRequest<JobPositionDto>;

public sealed class GetJobPositionByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetJobPositionByIdQuery, JobPositionDto>
{
    public async Task<JobPositionDto> Handle(GetJobPositionByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.JobPositions
            .AsNoTracking()
            .Where(p => p.Id == request.Id)
            .Select(p => new JobPositionDto { Id = p.Id, Code = p.Code, NameAr = p.NameAr, NameEn = p.NameEn, IsActive = p.IsActive, OrgUnitId = p.OrgUnitId, DefaultJobGradeId = p.DefaultJobGradeId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(JobPosition), request.Id);
    }
}
