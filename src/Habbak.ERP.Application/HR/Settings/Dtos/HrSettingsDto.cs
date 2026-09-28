using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Application.HR.Settings.Dtos;

/// <summary>Screen HR_SETTINGS — الحقول المرحلة 1 + Phase 3 (LeaveDayCountingMode) + Phase 4.6
/// (MonthBasis, DefaultCutoffDay, CompanyDefaultApproverUserId — Phase-4-Research.md §2.7) معروضة/قابلة
/// للتعديل من الشاشة؛ KioskSessionSeconds/SelfServiceClockInRequiresLocation (Phase 6) و
/// MaxAdvanceInstallmentPercent (Phase 5) لسه مؤجلة ومش جوه الـ Dto ده.</summary>
public sealed class HrSettingsDto
{
    public int DefaultProbationDays { get; init; }
    public long? DefaultBranchId { get; init; }
    public bool RequireNationalIdForActivation { get; init; }

    /// <summary>Phase 3 — طريقة حساب أيام LeaveRequest.Days (Calendar الافتراضي، أو WorkingDays).</summary>
    public LeaveDayCountingMode LeaveDayCountingMode { get; init; }

    /// <summary>قاعدة 34 — أساس حساب الشهر للنسبة والتناسب. null يعني لسه ماتحددش (⏸️ Pending Company،
    /// مفيش افتراضي صامت) — PayrollCalculationService بيعامله كـ ActualDays لو null (تفصيل في تعليق
    /// الكود هناك)، لكن الشاشة بتعرضه فاضي عمدًا عشان الشركة تحدده صراحة.</summary>
    public HrMonthBasis? MonthBasis { get; init; }

    public int? DefaultCutoffDay { get; init; }

    /// <summary>قاعدة 8 (§12.4) — المعتمد الاحتياطي لما ManagerId يبقى null أو المدير من غير حساب
    /// دخول. شغّال فعليًا في محرك الاعتمادات من Phase 2 (ApprovalStepResolutionService) — كان مخزّن
    /// بس من غير شاشة تحطه؛ فجوة استخدام حقيقية اتقفلت هنا (Phase-4-Research.md §2.7).</summary>
    public long? CompanyDefaultApproverUserId { get; init; }
}
