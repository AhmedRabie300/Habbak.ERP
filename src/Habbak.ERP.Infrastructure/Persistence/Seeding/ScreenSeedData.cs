using Habbak.ERP.Application.Common.Coding;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Docs/Implementation/Phase-2-Research.md §3.3 — seeds the unified <see cref="Screen"/> registry
/// (00-Project-Overview.md §12.2). Reuses <see cref="ScreenCodeCatalog"/> (already the single place
/// every "create new record" screen's code/NameAr/NameEn live) instead of retyping the same ~70
/// codes a second time, plus the handful of screens that exist as a [Screen(...)] attribute target
/// but never needed a coding rule (Employee hiring wizard, the two screens this phase itself adds).
/// Additive only, same philosophy as MenuItemSeedData — this is not every screen in the system yet,
/// only the ones something can actually attach a posting template or an approval workflow to so
/// far; a module adds its own screens here the same way whenever it needs to.
/// </summary>
public static class ScreenSeedData
{
    private static readonly (string Code, string NameAr, string NameEn, string ModuleCode)[] ExtraScreens =
    [
        ("HR_SETTINGS", "إعدادات شئون العاملين", "HR Settings", "HR"),
        ("HR_HIRING", "معالج التعيين", "Hiring Wizard", "HR"),
        ("SETTINGS_APPROVAL_WORKFLOWS", "سلاسل الاعتماد", "Approval Workflows", "SETTINGS"),
        ("APPROVAL_MY_PENDING", "بانتظار اعتمادي", "My Pending Approvals", "SETTINGS"),

        // Attendance & Leave (Docs/Implementation/HR-MASTER-PLAN.md §Phase 3) — كيانات غير مُرقَّمة
        // (مفيش عمود Code)، فمش موجودة في ScreenCodeCatalog زي HR_WORK_SHIFTS/HR_LEAVE_TYPES/HR_HOLIDAYS.
        ("HR_SHIFT_SCHEDULES", "جداول الورديات", "Shift Schedules", "HR"),
        ("HR_TIME_ENTRIES", "تسجيلات الحضور", "Time Entries", "HR"),
        ("HR_ATTENDANCE", "الحضور اليومي", "Daily Attendance", "HR"),
        ("HR_ATTENDANCE_CONFLICTS", "تعارضات الوردية مع الحضور", "Attendance Conflicts", "HR"),
        ("HR_LEAVE_BALANCES", "أرصدة الإجازات", "Leave Balances", "HR"),
        ("HR_LEAVE_REQUESTS", "طلبات الإجازات", "Leave Requests", "HR"),
        ("HR_OVERTIME", "طلبات الإضافي", "Overtime Requests", "HR"),
        // Remarks8 Item 7 — Phase 3 Amendment.
        ("HR_SHIFT_SCHEDULE_GENERATOR", "توليد جداول الورديات", "Shift Schedule Generator", "HR"),
    ];

    public static List<Screen> Build()
    {
        var screens = ScreenCodeCatalog.All
            .Select(s => new Screen
            {
                Code = s.ScreenCode,
                NameAr = s.LabelAr,
                NameEn = s.LabelEn,
                ModuleCode = ModuleCodeFor(s.ScreenCode)
            })
            .ToList();

        screens.AddRange(ExtraScreens.Select(s => new Screen
        {
            Code = s.Code, NameAr = s.NameAr, NameEn = s.NameEn, ModuleCode = s.ModuleCode
        }));

        return screens;
    }

    private static string ModuleCodeFor(string screenCode) => screenCode switch
    {
        _ when screenCode.StartsWith("ACCOUNTING_") => "ACCOUNTING",
        _ when screenCode.StartsWith("ORG_") => "ORG",
        _ when screenCode.StartsWith("INVENTORY_") => "INVENTORY",
        _ when screenCode.StartsWith("PURCHASING_") => "PURCHASING",
        _ when screenCode.StartsWith("SALES_") => "SALES",
        _ when screenCode.StartsWith("POS_") => "POS",
        _ when screenCode.StartsWith("FIXED_ASSETS") || screenCode.StartsWith("MAINTENANCE_") => "ASSETS_MAINTENANCE",
        _ when screenCode.StartsWith("HR_") => "HR",
        _ when screenCode.StartsWith("SETTINGS_") => "SETTINGS",
        _ => "GENERAL"
    };
}
