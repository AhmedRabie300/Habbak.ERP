using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Domain.Attendance;

/// <summary>سنوية، وعارضة، ومرضية، ووضع، وحج، وبدون أجر (§2.2). AnnualDays هو الـ Fallback الافتراضي
/// — القيمة الفعلية بتيجي من LeaveEntitlementRule (Phase 4) لو موجود.</summary>
public class LeaveType : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public LeaveAccrualMethod AccrualMethod { get; set; }
    public int AnnualDays { get; set; }
    public int MaxCarryOver { get; set; }
    public bool IsPaid { get; set; } = true;
    public decimal PaidPercentage { get; set; } = 100;

    public long? DeductFromLeaveTypeId { get; set; }
    public LeaveType? DeductFromLeaveType { get; set; }

    public bool RequiresDocument { get; set; }
    public int? MaxDaysPerRequest { get; set; }
    public Gender? GenderRestriction { get; set; }
    public int? MaxTimesInService { get; set; }
    public bool IsCashableOnTermination { get; set; }
}

/// <summary>صف لكل موظف ونوع وسنة (§2.2). <see cref="Pending"/> حجز مؤقت بيتغيّر مباشرة على الصف
/// نفسه (بدون History، Phase-3-Research.md §3.6) — <see cref="Used"/> وحدها لازم صف
/// LeaveBalanceHistory لأي تغيير (قاعدة 26). Available محسوبة، مش عمود.</summary>
public class LeaveBalance : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public int Year { get; set; }
    public decimal AccruedThisYear { get; set; }
    public decimal CarriedOver { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }

    public decimal Available => AccruedThisYear + CarriedOver - Used - Pending;
}

/// <summary>سجل كل حركة نهائية على الرصيد (قاعدة 26) — الرصيد = مجموعه دايمًا.</summary>
public class LeaveBalanceHistory : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long LeaveBalanceId { get; set; }
    public LeaveBalance LeaveBalance { get; set; } = null!;

    public LeaveBalanceMovementType MovementType { get; set; }
    public decimal Days { get; set; }
    public DateOnly EffectiveDate { get; set; }

    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>قاعدة 22-27. Days: عدّ تقويمي شامل افتراضيًا (Calendar)، أو أيام عمل فعلية
/// (WorkingDays) حسب HrSettings.LeaveDayCountingMode (Phase-3-Research.md §3.8، قرار المستخدم).
/// ApprovalInstance.EntityType = Screen.Code مباشرة ("HR_LEAVE_REQUESTS").</summary>
public class LeaveRequest : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    public string? Reason { get; set; }

    public HrRequestStatus Status { get; set; } = HrRequestStatus.Draft;
    public long? ApprovalInstanceId { get; set; }
}

/// <summary>إدخال سنوي (§2.2). BranchId اختياري فعلًا (null = يسري على كل الفروع) — عكس نمط
/// Employee/EmploymentContract (إلزامي بالـDB)، فبيستفيد من فلتر IBranchScopedEntity زي ما هو
/// ("رؤية الفرع الحالي + اللي بلا فرع"). <see cref="EndDate"/> = null يعني عطلة يوم واحد
/// (Remarks8 Item 8 — عيد الفطر من... إلى...).</summary>
public class Holiday : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Year { get; set; }
    public bool IsNational { get; set; } = true;
}

/// <summary>موافقة مسبقة (§2.2، قاعدة 17) — خطوة اعتماد واحدة بس في Phase 3 (Phase-3-Research.md
/// §3.7)؛ خطوة "فوق الحد الشهري" مؤجّلة لـ Phase 4 (الجدول القانوني نفسه لسه مش موجود).</summary>
public class OvertimeRequest : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly WorkDate { get; set; }
    public int PlannedMinutes { get; set; }
    public OvertimeType OvertimeType { get; set; }
    public string? Reason { get; set; }

    public HrRequestStatus Status { get; set; } = HrRequestStatus.Draft;
    public long? ApprovalInstanceId { get; set; }

    /// <summary>بيتملى من Attendance.RecomputeDaily لما فيه طلب Approved لنفس اليوم.</summary>
    public int? ActualMinutes { get; set; }
}
