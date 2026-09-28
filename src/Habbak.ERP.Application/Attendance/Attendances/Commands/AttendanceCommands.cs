using FluentValidation;
using Habbak.ERP.Application.Attendance.Holidays;
using Habbak.ERP.Application.Attendance.ShiftSchedules;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AttendanceEntity = Habbak.ERP.Domain.Attendance.Attendance;

namespace Habbak.ERP.Application.Attendance.Attendances.Commands;

/// <summary>
/// قاعدة 14 — الملخص اليومي محسوب من TimeEntry المقبولة (Accepted بس)، مش يدوي. الأولوية:
/// RestDay (جدول) ثم Holiday ثم Leave معتمدة ثم Present/Absent فعليًا حسب وجود تسجيلات.
/// كمان بيصالح OvertimeRequest.ActualMinutes لو فيه طلب Approved لنفس اليوم (Phase-3-Research.md).
/// إعادة الحساب بتصفّر IsApproved — أي إعادة حساب محتاجة اعتماد مشرف تاني.
/// </summary>
public sealed record RecomputeDailyAttendanceCommand(long EmployeeId, DateOnly WorkDate) : IRequest<long>;

public sealed class RecomputeDailyAttendanceCommandValidator : AbstractValidator<RecomputeDailyAttendanceCommand>
{
    public RecomputeDailyAttendanceCommandValidator() => RuleFor(x => x.EmployeeId).GreaterThan(0);
}

public sealed class RecomputeDailyAttendanceCommandHandler(IApplicationDbContext db) : IRequestHandler<RecomputeDailyAttendanceCommand, long>
{
    public async Task<long> Handle(RecomputeDailyAttendanceCommand request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var dayStart = request.WorkDate.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var entries = await db.TimeEntries
            .Where(t => t.EmployeeId == request.EmployeeId && t.Status == TimeEntryStatus.Accepted
                        && t.TimestampUtc >= dayStart && t.TimestampUtc < dayEnd)
            .ToListAsync(cancellationToken);

        var schedule = await ShiftScheduleLookup.FindForDateAsync(db, request.EmployeeId, request.WorkDate, cancellationToken);

        var isHoliday = await HolidayLookup.IsHolidayAsync(db, request.WorkDate, employee.BranchId, cancellationToken);

        var isOnApprovedLeave = await db.LeaveRequests.AnyAsync(
            r => r.EmployeeId == request.EmployeeId && r.Status == HrRequestStatus.Approved
                 && r.StartDate <= request.WorkDate && r.EndDate >= request.WorkDate, cancellationToken);

        var firstIn = entries.Where(e => e.EntryType == TimeEntryType.In).Select(e => e.TimestampUtc).DefaultIfEmpty().Min();
        var lastOut = entries.Where(e => e.EntryType == TimeEntryType.Out).Select(e => e.TimestampUtc).DefaultIfEmpty().Max();
        var hasFirstIn = firstIn != default;
        var hasLastOut = lastOut != default;

        AttendanceStatus status;
        var workedMinutes = 0;
        var lateMinutes = 0;
        var earlyLeaveMinutes = 0;
        var overtimeMinutes = 0;

        if (schedule?.IsRestDay == true && !hasFirstIn && !hasLastOut)
        {
            status = AttendanceStatus.RestDay;
        }
        else if (isHoliday && !hasFirstIn && !hasLastOut)
        {
            status = AttendanceStatus.Holiday;
        }
        else if (isOnApprovedLeave && !hasFirstIn && !hasLastOut)
        {
            status = AttendanceStatus.Leave;
        }
        else if (!hasFirstIn && !hasLastOut)
        {
            status = AttendanceStatus.Absent;
        }
        else
        {
            status = AttendanceStatus.Present;
            if (hasFirstIn && hasLastOut && lastOut > firstIn)
            {
                var breakMinutes = schedule?.WorkShiftDefinition?.BreakMinutes ?? 0;
                workedMinutes = Math.Max(0, (int)(lastOut - firstIn).TotalMinutes - breakMinutes);
            }

            if (schedule?.WorkShiftDefinition is { } def)
            {
                var scheduledStart = request.WorkDate.ToDateTime(def.StartTime);
                var scheduledEnd = def.EndTime < def.StartTime ? request.WorkDate.AddDays(1).ToDateTime(def.EndTime) : request.WorkDate.ToDateTime(def.EndTime);
                var scheduledMinutes = Math.Max(0, (int)(scheduledEnd - scheduledStart).TotalMinutes - def.BreakMinutes);

                if (hasFirstIn && firstIn > scheduledStart) lateMinutes = (int)(firstIn - scheduledStart).TotalMinutes;
                if (hasLastOut && lastOut < scheduledEnd) earlyLeaveMinutes = (int)(scheduledEnd - lastOut).TotalMinutes;
                overtimeMinutes = Math.Max(0, workedMinutes - scheduledMinutes);
            }
        }

        var attendance = await db.Attendances.FirstOrDefaultAsync(
            a => a.EmployeeId == request.EmployeeId && a.WorkDate == request.WorkDate, cancellationToken);

        if (attendance is null)
        {
            attendance = new AttendanceEntity { EmployeeId = request.EmployeeId, WorkDate = request.WorkDate };
            db.Attendances.Add(attendance);
        }

        attendance.CompanyId = employee.CompanyId;
        attendance.BranchId = employee.BranchId;
        attendance.ShiftScheduleId = schedule?.Id;
        attendance.FirstInUtc = hasFirstIn ? firstIn : null;
        attendance.LastOutUtc = hasLastOut ? lastOut : null;
        attendance.WorkedMinutes = workedMinutes;
        attendance.LateMinutes = lateMinutes;
        attendance.EarlyLeaveMinutes = earlyLeaveMinutes;
        attendance.OvertimeMinutes = overtimeMinutes;
        attendance.Status = status;
        attendance.IsApproved = false;

        var approvedOvertime = await db.OvertimeRequests.FirstOrDefaultAsync(
            o => o.EmployeeId == request.EmployeeId && o.WorkDate == request.WorkDate && o.Status == HrRequestStatus.Approved, cancellationToken);
        if (approvedOvertime is not null)
        {
            approvedOvertime.ActualMinutes = Math.Min(overtimeMinutes, approvedOvertime.PlannedMinutes);
        }

        await db.SaveChangesAsync(cancellationToken);
        return attendance.Id;
    }
}

public sealed record ApproveAttendanceCommand(long Id) : IRequest;

public sealed class ApproveAttendanceCommandHandler(IApplicationDbContext db) : IRequestHandler<ApproveAttendanceCommand>
{
    public async Task Handle(ApproveAttendanceCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Attendances.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Attendance", request.Id);

        entity.IsApproved = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
