using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules.Queries;

/// <summary>مش Paginated — جدول الأسبوع لفرع/موظف بحجم محدود دايمًا (نفس منطق GetJobPositionsListQuery).
/// الفلترة بالتاريخ بقت Range Overlap (Remarks8 Item 5)، مش مساواة يوم بيوم.</summary>
public sealed record GetShiftSchedulesListQuery(long? EmployeeId, DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<ShiftScheduleDto>>;

public sealed class GetShiftSchedulesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetShiftSchedulesListQuery, IReadOnlyList<ShiftScheduleDto>>
{
    public async Task<IReadOnlyList<ShiftScheduleDto>> Handle(GetShiftSchedulesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.ShiftSchedules.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(s => s.EmployeeId == request.EmployeeId);
        // Range Overlap: صف يظهر لو (بدايته <= لغاية المطلوب) و(نهايته أو بدايته لو يوم واحد >= من المطلوب).
        if (request.ToDate is not null) query = query.Where(s => s.StartDate <= request.ToDate);
        if (request.FromDate is not null) query = query.Where(s => (s.EndDate ?? s.StartDate) >= request.FromDate);

        return await query.OrderBy(s => s.StartDate).Select(ToDto).ToListAsync(cancellationToken);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<ShiftSchedule, ShiftScheduleDto>> ToDto = s => new ShiftScheduleDto
    {
        Id = s.Id, EmployeeId = s.EmployeeId, BranchId = s.BranchId, StartDate = s.StartDate, EndDate = s.EndDate,
        WorkShiftDefinitionId = s.WorkShiftDefinitionId, IsRestDay = s.IsRestDay
    };
}

public sealed record GetShiftScheduleByIdQuery(long Id) : IRequest<ShiftScheduleDto>;

public sealed class GetShiftScheduleByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetShiftScheduleByIdQuery, ShiftScheduleDto>
{
    public async Task<ShiftScheduleDto> Handle(GetShiftScheduleByIdQuery request, CancellationToken cancellationToken) =>
        await db.ShiftSchedules.AsNoTracking().Where(s => s.Id == request.Id).Select(GetShiftSchedulesListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(ShiftSchedule), request.Id);
}
