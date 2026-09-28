using Habbak.ERP.Application.Attendance.Holidays;
using Habbak.ERP.Application.Attendance.ShiftSchedules;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveRequests;

/// <summary>
/// Docs/Implementation/Phase-3-Research.md §3.8 + قرار المستخدم عند اعتماد Phase 3 (LeaveDayCountingMode).
/// مُستخدَمة من CreateLeaveRequestCommand ومن CalculateLeaveDaysQuery (Preview) — نفس المنطق بالظبط
/// عشان الطلب الفعلي يطابق المعاينة اللي المستخدم شافها في الـ Frontend.
/// </summary>
public static class LeaveDaysCalculator
{
    public sealed record Result(decimal Days, LeaveDayCountingMode Mode, bool UsedFallback);

    public static async Task<Result> CalculateAsync(
        IApplicationDbContext db, long employeeId, long? companyId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var mode = await db.HrSettingsRows.Where(s => s.CompanyId == companyId)
            .Select(s => (LeaveDayCountingMode?)s.LeaveDayCountingMode)
            .FirstOrDefaultAsync(cancellationToken) ?? LeaveDayCountingMode.Calendar;

        var calendarDays = (decimal)(endDate.DayNumber - startDate.DayNumber + 1);
        if (mode == LeaveDayCountingMode.Calendar)
        {
            return new Result(calendarDays, mode, UsedFallback: false);
        }

        var hasAnySchedule = await db.ShiftSchedules.AnyAsync(s => s.EmployeeId == employeeId, cancellationToken);
        if (!hasAnySchedule)
        {
            // Edge Case (طلب المستخدم) — ShiftSchedule ناقص خالص لهذا الموظف: Fallback لـCalendar + تحذير.
            return new Result(calendarDays, LeaveDayCountingMode.Calendar, UsedFallback: true);
        }

        var dates = Enumerable.Range(0, endDate.DayNumber - startDate.DayNumber + 1).Select(startDate.AddDays).ToList();

        // Remarks8 Item 5 — ShiftSchedule فترات (Ranges) دلوقتي، فبنجيب كل الصفوف المتقاطعة مع
        // الفترة المطلوبة مرة واحدة، وبعدين بنفكّها يوم بيوم في الذاكرة.
        var overlapping = await ShiftScheduleLookup.FindOverlappingAsync(db, employeeId, startDate, endDate, cancellationToken);
        var restDays = dates.Where(d => overlapping.Any(s => s.IsRestDay && s.StartDate <= d && (s.EndDate ?? s.StartDate) >= d)).ToHashSet();

        var employee = await db.Employees.Where(e => e.Id == employeeId).Select(e => new { e.BranchId }).FirstOrDefaultAsync(cancellationToken);
        var holidays = await HolidayLookup.ExpandOverlappingAsync(db, startDate, endDate, employee?.BranchId, cancellationToken);

        var workingDays = dates.Count(d => !restDays.Contains(d) && !holidays.Contains(d));
        return new Result(workingDays, mode, UsedFallback: false);
    }
}
