using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules;

/// <summary>
/// Remarks8 Item 5 — ShiftSchedule بقت فترة (StartDate → EndDate؟)، فأي استعلام "هل اليوم ده
/// كذا؟" لازم يتحقق من التداخل (Range Containment)، مش مساواة يوم بيوم. مُستخدَمة من
/// RecomputeDailyAttendanceCommandHandler وLeaveDaysCalculator عشان الاتنين ميختلفوش في التفسير.
/// </summary>
public static class ShiftScheduleLookup
{
    /// <summary>الصف اللي بيغطّي اليوم ده لموظف بعينه، لو موجود. لو أكتر من صف متراكب (مايفترضش
    /// يحصل عادةً، بس مش ممنوع على مستوى الـDB)، الأحدث StartDate بيفوز.</summary>
    public static Task<ShiftSchedule?> FindForDateAsync(IApplicationDbContext db, long employeeId, DateOnly date, CancellationToken cancellationToken) =>
        db.ShiftSchedules
            .Where(s => s.EmployeeId == employeeId && s.StartDate <= date && (s.EndDate ?? s.StartDate) >= date)
            .Include(s => s.WorkShiftDefinition)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>كل صفوف الفترة لموظف بعينه بتتقاطع مع [fromDate, toDate] — مُستخدَمة لما محتاجين
    /// نفحص عدة أيام مرة واحدة (LeaveDaysCalculator) بدل استعلام لكل يوم.</summary>
    public static Task<List<ShiftSchedule>> FindOverlappingAsync(
        IApplicationDbContext db, long employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        db.ShiftSchedules
            .Where(s => s.EmployeeId == employeeId && s.StartDate <= toDate && (s.EndDate ?? s.StartDate) >= fromDate)
            .ToListAsync(cancellationToken);

    /// <summary>هل فترتين (Start1..End1) و(Start2..End2) بتتقاطعوا؟ End = null يعني يوم واحد (Start).</summary>
    public static bool RangesOverlap(DateOnly start1, DateOnly? end1, DateOnly start2, DateOnly? end2) =>
        start1 <= (end2 ?? start2) && start2 <= (end1 ?? start1);
}
