using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AttendanceEntity = Habbak.ERP.Domain.Attendance.Attendance;

namespace Habbak.ERP.Application.Attendance.Attendances.Queries;

public sealed record GetAttendancesListQuery(long? EmployeeId, DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetAttendancesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAttendancesListQuery, IReadOnlyList<AttendanceDto>>
{
    public async Task<IReadOnlyList<AttendanceDto>> Handle(GetAttendancesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Attendances.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(a => a.EmployeeId == request.EmployeeId);
        if (request.FromDate is not null) query = query.Where(a => a.WorkDate >= request.FromDate);
        if (request.ToDate is not null) query = query.Where(a => a.WorkDate <= request.ToDate);

        return await query.OrderByDescending(a => a.WorkDate).Select(ToDto).ToListAsync(cancellationToken);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<AttendanceEntity, AttendanceDto>> ToDto = a => new AttendanceDto
    {
        Id = a.Id, EmployeeId = a.EmployeeId, WorkDate = a.WorkDate, ShiftScheduleId = a.ShiftScheduleId,
        FirstInUtc = a.FirstInUtc, LastOutUtc = a.LastOutUtc, WorkedMinutes = a.WorkedMinutes, LateMinutes = a.LateMinutes,
        EarlyLeaveMinutes = a.EarlyLeaveMinutes, OvertimeMinutes = a.OvertimeMinutes, Status = a.Status, IsApproved = a.IsApproved
    };
}

public sealed record GetAttendanceByIdQuery(long Id) : IRequest<AttendanceDto>;

public sealed class GetAttendanceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAttendanceByIdQuery, AttendanceDto>
{
    public async Task<AttendanceDto> Handle(GetAttendanceByIdQuery request, CancellationToken cancellationToken) =>
        await db.Attendances.AsNoTracking().Where(a => a.Id == request.Id).Select(GetAttendancesListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Attendance", request.Id);
}

/// <summary>نطاق Self — الموظف المرتبط بالمستخدم الحالي (Employee.UserId، قسم 12.4/00-Frontend-Specs §18 نفس المبدأ).</summary>
public sealed record GetMyAttendanceQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetMyAttendanceQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetMyAttendanceQuery, IReadOnlyList<AttendanceDto>>
{
    public async Task<IReadOnlyList<AttendanceDto>> Handle(GetMyAttendanceQuery request, CancellationToken cancellationToken)
    {
        if (current.EmployeeId is not { } employeeId)
        {
            return [];
        }

        var query = db.Attendances.AsNoTracking().Where(a => a.EmployeeId == employeeId);
        if (request.FromDate is not null) query = query.Where(a => a.WorkDate >= request.FromDate);
        if (request.ToDate is not null) query = query.Where(a => a.WorkDate <= request.ToDate);

        return await query.OrderByDescending(a => a.WorkDate).Select(GetAttendancesListQueryHandler.ToDto).ToListAsync(cancellationToken);
    }
}

/// <summary>نطاق Team — الموظفين اللي ManagerId بتاعهم = موظف المستخدم الحالي (تقارير مباشرة بس، مش هرمية كاملة).</summary>
public sealed record GetTeamAttendanceQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<AttendanceDto>>;

public sealed class GetTeamAttendanceQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetTeamAttendanceQuery, IReadOnlyList<AttendanceDto>>
{
    public async Task<IReadOnlyList<AttendanceDto>> Handle(GetTeamAttendanceQuery request, CancellationToken cancellationToken)
    {
        if (current.EmployeeId is not { } managerId)
        {
            return [];
        }

        var teamIds = await db.Employees.Where(e => e.ManagerId == managerId).Select(e => e.Id).ToListAsync(cancellationToken);
        if (teamIds.Count == 0)
        {
            return [];
        }

        var query = db.Attendances.AsNoTracking().Where(a => teamIds.Contains(a.EmployeeId));
        if (request.FromDate is not null) query = query.Where(a => a.WorkDate >= request.FromDate);
        if (request.ToDate is not null) query = query.Where(a => a.WorkDate <= request.ToDate);

        return await query.OrderBy(a => a.EmployeeId).ThenByDescending(a => a.WorkDate).Select(GetAttendancesListQueryHandler.ToDto).ToListAsync(cancellationToken);
    }
}

public sealed record GetEmployeeAttendanceReportQuery(long EmployeeId, DateOnly FromDate, DateOnly ToDate) : IRequest<IReadOnlyList<EmployeeAttendanceReportLineDto>>;

public sealed class GetEmployeeAttendanceReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeAttendanceReportQuery, IReadOnlyList<EmployeeAttendanceReportLineDto>>
{
    public async Task<IReadOnlyList<EmployeeAttendanceReportLineDto>> Handle(GetEmployeeAttendanceReportQuery request, CancellationToken cancellationToken) =>
        await db.Attendances.AsNoTracking()
            .Where(a => a.EmployeeId == request.EmployeeId && a.WorkDate >= request.FromDate && a.WorkDate <= request.ToDate)
            .OrderBy(a => a.WorkDate)
            .Select(a => new EmployeeAttendanceReportLineDto
            {
                WorkDate = a.WorkDate, Status = a.Status, FirstInUtc = a.FirstInUtc, LastOutUtc = a.LastOutUtc,
                WorkedMinutes = a.WorkedMinutes, LateMinutes = a.LateMinutes, EarlyLeaveMinutes = a.EarlyLeaveMinutes,
                OvertimeMinutes = a.OvertimeMinutes, IsApproved = a.IsApproved
            })
            .ToListAsync(cancellationToken);
}

/// <summary>قاعدة 12 — كاشير فاتح وردية POS.Shift وحضوره Absent (أو مفيش سجل حضور خالص) لنفس اليوم.
/// Screen.Code منفصل HR_ATTENDANCE_CONFLICTS (§5.1 رقم 9).</summary>
public sealed record GetAttendanceConflictsQuery(DateOnly Date, long? BranchId) : IRequest<IReadOnlyList<AttendanceConflictDto>>;

public sealed class GetAttendanceConflictsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAttendanceConflictsQuery, IReadOnlyList<AttendanceConflictDto>>
{
    public async Task<IReadOnlyList<AttendanceConflictDto>> Handle(GetAttendanceConflictsQuery request, CancellationToken cancellationToken)
    {
        var dayStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var shiftsQuery = db.Shifts.AsNoTracking()
            .Where(s => s.OpenedAtUtc < dayEnd && (s.ClosedAtUtc == null || s.ClosedAtUtc >= dayStart));
        if (request.BranchId is not null)
        {
            shiftsQuery = shiftsQuery.Where(s => s.BranchId == request.BranchId);
        }

        var shifts = await shiftsQuery.Select(s => new { s.Id, s.CashierUserId }).ToListAsync(cancellationToken);
        if (shifts.Count == 0)
        {
            return [];
        }

        var userIds = shifts.Select(s => s.CashierUserId).Distinct().ToList();
        var employeesByUserId = await db.Employees.AsNoTracking()
            .Where(e => e.UserId != null && userIds.Contains(e.UserId!.Value))
            .Select(e => new { e.Id, e.UserId, e.Code, e.NameAr })
            .ToListAsync(cancellationToken);

        var attendanceByEmployee = await db.Attendances.AsNoTracking()
            .Where(a => a.WorkDate == request.Date)
            .Select(a => new { a.EmployeeId, a.Status })
            .ToListAsync(cancellationToken);
        var attendanceMap = attendanceByEmployee.ToDictionary(a => a.EmployeeId, a => a.Status);

        var conflicts = new List<AttendanceConflictDto>();
        foreach (var shift in shifts)
        {
            var employee = employeesByUserId.FirstOrDefault(e => e.UserId == shift.CashierUserId);
            if (employee is null)
            {
                // مفيش موظف مربوط بالكاشير ده — Silent Ignore زي التلقيم نفسه (Phase-3-Research.md §3.2)، خارج نطاق هذا التقرير.
                continue;
            }

            var isAbsent = !attendanceMap.TryGetValue(employee.Id, out var status) || status == AttendanceStatus.Absent;
            if (!isAbsent)
            {
                continue;
            }

            conflicts.Add(new AttendanceConflictDto
            {
                EmployeeId = employee.Id,
                EmployeeCode = employee.Code,
                EmployeeNameAr = employee.NameAr,
                WorkDate = request.Date,
                POSShiftId = shift.Id,
                Description = "كاشير فاتح وردية POS ولكن حضوره اليوم غير مسجّل/غائب."
            });
        }

        return conflicts;
    }
}
