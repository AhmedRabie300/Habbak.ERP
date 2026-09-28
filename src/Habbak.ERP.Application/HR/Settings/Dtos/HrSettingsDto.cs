using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Application.HR.Settings.Dtos;

/// <summary>Screen HR_SETTINGS — الحقول المرحلة 1 بس معروضة/قابلة للتعديل من الشاشة؛ باقي حقول
/// الكيان (MonthBasis, DefaultCutoffDay, KioskSessionSeconds, SelfServiceClockInRequiresLocation,
/// CompanyDefaultApproverUserId, MaxAdvanceInstallmentPercent) مؤجلة لمراحلها ومش جوه الـ Dto ده.</summary>
public sealed class HrSettingsDto
{
    public int DefaultProbationDays { get; init; }
    public long? DefaultBranchId { get; init; }
    public bool RequireNationalIdForActivation { get; init; }

    /// <summary>Phase 3 — طريقة حساب أيام LeaveRequest.Days (Calendar الافتراضي، أو WorkingDays).</summary>
    public LeaveDayCountingMode LeaveDayCountingMode { get; init; }
}
