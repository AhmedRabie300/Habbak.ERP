using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.WorkShiftDefinitions.Queries;

public sealed record GetWorkShiftDefinitionsListQuery : IRequest<IReadOnlyList<WorkShiftDefinitionDto>>;

public sealed class GetWorkShiftDefinitionsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWorkShiftDefinitionsListQuery, IReadOnlyList<WorkShiftDefinitionDto>>
{
    public async Task<IReadOnlyList<WorkShiftDefinitionDto>> Handle(GetWorkShiftDefinitionsListQuery request, CancellationToken cancellationToken) =>
        await db.WorkShiftDefinitions.AsNoTracking().OrderBy(w => w.Code)
            .Select(w => new WorkShiftDefinitionDto
            {
                Id = w.Id, Code = w.Code, NameAr = w.NameAr, NameEn = w.NameEn, IsActive = w.IsActive,
                StartTime = w.StartTime, EndTime = w.EndTime, BreakMinutes = w.BreakMinutes, IsNightShift = w.IsNightShift
            })
            .ToListAsync(cancellationToken);
}

public sealed record GetWorkShiftDefinitionByIdQuery(long Id) : IRequest<WorkShiftDefinitionDto>;

public sealed class GetWorkShiftDefinitionByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWorkShiftDefinitionByIdQuery, WorkShiftDefinitionDto>
{
    public async Task<WorkShiftDefinitionDto> Handle(GetWorkShiftDefinitionByIdQuery request, CancellationToken cancellationToken) =>
        await db.WorkShiftDefinitions.AsNoTracking().Where(w => w.Id == request.Id)
            .Select(w => new WorkShiftDefinitionDto
            {
                Id = w.Id, Code = w.Code, NameAr = w.NameAr, NameEn = w.NameEn, IsActive = w.IsActive,
                StartTime = w.StartTime, EndTime = w.EndTime, BreakMinutes = w.BreakMinutes, IsNightShift = w.IsNightShift
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(WorkShiftDefinition), request.Id);
}
