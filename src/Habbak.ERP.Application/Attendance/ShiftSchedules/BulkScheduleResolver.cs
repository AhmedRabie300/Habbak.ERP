using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.ShiftSchedules;

/// <summary>Remarks8 Item 7 — استثناء موظف بعينه (شفت/راحة مختلفة) عن إعدادات المولّد الجماعي الافتراضية.</summary>
public sealed record BulkScheduleExceptionInput(long EmployeeId, long? WorkShiftDefinitionId, int? WeeklyRestDaysMask);

/// <summary>الخطة الفعلية لموظف واحد بعد تطبيق الاستثناءات فوق الإعدادات الافتراضية.</summary>
public sealed record ResolvedEmployeePlan(long EmployeeId, string EmployeeCode, string EmployeeNameAr, long? EffectiveWorkShiftDefinitionId, int EffectiveWeeklyRestDaysMask);

/// <summary>
/// Remarks8 Item 7 — منطق مشترك بين GenerateBulkShiftSchedulesCommand وPreviewBulkShiftSchedulesQuery
/// (نفس القرار بالظبط في الاتنين): حل الموظفين تحت OrgUnit (شامل الفروع التابعة)، وتطبيق أي
/// استثناء موظف بعينه فوق الإعدادات الافتراضية.
/// </summary>
public static class BulkScheduleResolver
{
    public static async Task<List<ResolvedEmployeePlan>> ResolveEmployeePlansAsync(
        IApplicationDbContext db, long? companyId, long orgUnitId, long? defaultWorkShiftDefinitionId, int defaultWeeklyRestDaysMask,
        IReadOnlyList<BulkScheduleExceptionInput>? exceptions, CancellationToken cancellationToken)
    {
        var orgUnitIds = await ResolveDescendantOrgUnitIdsAsync(db, companyId, orgUnitId, cancellationToken);

        var employees = await db.Employees
            .Where(e => e.CompanyId == companyId && orgUnitIds.Contains(e.OrgUnitId) && e.Status == EmployeeStatus.Active)
            .Select(e => new { e.Id, e.Code, e.NameAr })
            .ToListAsync(cancellationToken);

        var exceptionMap = (exceptions ?? []).ToDictionary(x => x.EmployeeId);

        return employees.Select(e =>
        {
            var exception = exceptionMap.GetValueOrDefault(e.Id);
            return new ResolvedEmployeePlan(
                e.Id, e.Code, e.NameAr,
                exception?.WorkShiftDefinitionId ?? defaultWorkShiftDefinitionId,
                exception?.WeeklyRestDaysMask ?? defaultWeeklyRestDaysMask);
        }).ToList();
    }

    /// <summary>الـ OrgUnit المختار + كل الفروع/الأقسام التابعة له (BFS فوق ParentId) — "لـ الفروع"
    /// في صياغة المستخدم يعني اختيار فرع/إدارة أعلى وتوليد الجدول لكل التابعين له تلقائيًا.</summary>
    private static async Task<HashSet<long>> ResolveDescendantOrgUnitIdsAsync(IApplicationDbContext db, long? companyId, long rootId, CancellationToken cancellationToken)
    {
        var all = await db.OrgUnits.Where(u => u.CompanyId == companyId).Select(u => new { u.Id, u.ParentId }).ToListAsync(cancellationToken);
        var childrenByParent = all.Where(u => u.ParentId != null).ToLookup(u => u.ParentId!.Value);

        var result = new HashSet<long> { rootId };
        var queue = new Queue<long>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in childrenByParent[current])
            {
                if (result.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    public static bool IsRestDay(int weeklyRestDaysMask, DateOnly date) => (weeklyRestDaysMask & (1 << (int)date.DayOfWeek)) != 0;
}
