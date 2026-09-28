using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>أساس حساب الشهر في معادلة الاستحقاق بالتناسب (قاعدة 34، `10-Module-HR-Payroll.md:247`) —
/// مفيش افتراضي صامت، القيمة إلزامية عند إعداد الشركة (Pending Company، تُفتح شاشتها في Phase 4).</summary>
public enum HrMonthBasis
{
    ActualDays = 1,
    Thirty = 2
}

/// <summary>Docs/Implementation/Phase-3-Research.md §3.8 + طلب المستخدم عند اعتماد Phase 3 — طريقة
/// حساب أيام LeaveRequest.Days. Calendar (الافتراضي): عدّ تقويمي شامل. WorkingDays: بدون أيام
/// الراحة (ShiftSchedule.IsRestDay) والعطلات (Holiday) — لو ShiftSchedule ناقص لليوم، الحساب
/// بيرجع لـCalendar لنفس اليوم مع تحذير (Fallback، مش رفض).</summary>
public enum LeaveDayCountingMode
{
    Calendar = 1,
    WorkingDays = 2
}

/// <summary>
/// إعدادات HR على مستوى الشركة (Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.0) —
/// صف واحد لكل شركة، بنفس نمط PurchaseCycleSettings/BranchPOSSettings. الكيان يُبنى بكل الحقول من
/// الأول (نفس فكرة BranchPOSSettings) لكن شاشة HR_SETTINGS في هذه المرحلة بتعرض وتعدّل حقول المرحلة 1
/// بس (DefaultProbationDays/DefaultBranchId/RequireNationalIdForActivation) — باقي الحقول Nullable
/// بدون شاشة لحد ما مرحلتها تفتحها.
/// </summary>
public class HrSettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    // مرحلة 1 — معروضة في الشاشة
    public int DefaultProbationDays { get; set; } = 90;
    public long? DefaultBranchId { get; set; }
    public bool RequireNationalIdForActivation { get; set; } = true;

    // مؤجلة — الكيان بس، بدون شاشة (مرحلة الرواتب)
    public HrMonthBasis? MonthBasis { get; set; }
    public int? DefaultCutoffDay { get; set; }

    // مؤجلة — الكيان بس، بدون شاشة (مرحلة الخدمة الذاتية)
    public int? KioskSessionSeconds { get; set; }
    public bool SelfServiceClockInRequiresLocation { get; set; }

    // مؤجلة — الكيان بس، بدون شاشة (مرحلة محرك الاعتمادات)
    public long? CompanyDefaultApproverUserId { get; set; }

    // مؤجلة — الكيان بس، بدون شاشة (مرحلة السلف)
    public decimal? MaxAdvanceInstallmentPercent { get; set; }

    // Phase 3 — معروضة في شاشة HR_SETTINGS (Dropdown طريقة حساب أيام الإجازة)
    public LeaveDayCountingMode LeaveDayCountingMode { get; set; } = LeaveDayCountingMode.Calendar;
}
