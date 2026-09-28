using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.Holidays;

/// <summary>Remarks8 Item 8 — Holiday بقت فترة (StartDate → EndDate؟ = عيد الفطر من... إلى...)،
/// فأي فحص "هل اليوم ده عطلة؟" لازم يتحقق من التداخل مش مساواة يوم بيوم.</summary>
public static class HolidayLookup
{
    public static Task<bool> IsHolidayAsync(IApplicationDbContext db, DateOnly date, long? branchId, CancellationToken cancellationToken) =>
        db.Holidays.AnyAsync(
            h => h.IsActive && h.StartDate <= date && (h.EndDate ?? h.StartDate) >= date && (h.BranchId == null || h.BranchId == branchId),
            cancellationToken);

    /// <summary>كل أيام العطلات (مُفكَّكة يوم بيوم) اللي بتتقاطع مع [fromDate, toDate] لفرع بعينه —
    /// مُستخدَمة من LeaveDaysCalculator (WorkingDays mode).</summary>
    public static async Task<HashSet<DateOnly>> ExpandOverlappingAsync(
        IApplicationDbContext db, DateOnly fromDate, DateOnly toDate, long? branchId, CancellationToken cancellationToken)
    {
        var holidays = await db.Holidays
            .Where(h => h.IsActive && h.StartDate <= toDate && (h.EndDate ?? h.StartDate) >= fromDate && (h.BranchId == null || h.BranchId == branchId))
            .Select(h => new { h.StartDate, h.EndDate })
            .ToListAsync(cancellationToken);

        var days = new HashSet<DateOnly>();
        foreach (var h in holidays)
        {
            var start = h.StartDate < fromDate ? fromDate : h.StartDate;
            var end = (h.EndDate ?? h.StartDate) > toDate ? toDate : (h.EndDate ?? h.StartDate);
            for (var d = start; d <= end; d = d.AddDays(1))
            {
                days.Add(d);
            }
        }

        return days;
    }
}
