using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.POS;

namespace Habbak.ERP.Domain.Attendance;

/// <summary>
/// Docs/Modules/10-Module-HR-Payroll.md §2.2. مختلف تمامًا عن POS.Shift (وردية الكاشير المالية) —
/// دي وردية عمل (توقيت حضور) لكل موظف/فرع.
/// </summary>
public class WorkShiftDefinition : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public bool IsNightShift { get; set; }
}

/// <summary>جدول الأسبوع لكل فرع (§2.2) — صف بيغطّي فترة (StartDate → EndDate)، مش يوم واحد بس
/// (Remarks8 Item 5 — Phase 3 Amendment). <see cref="EndDate"/> = null يعني يوم واحد
/// (StartDate بس). القيم (WorkShiftDefinitionId/IsRestDay) موحّدة على طول الفترة —
/// نمط مختلف بيتطلب صف تاني (ده اللي بيحصل تلقائيًا في المولّد الجماعي، Item 7).</summary>
public class ShiftSchedule : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public long? WorkShiftDefinitionId { get; set; }
    public WorkShiftDefinition? WorkShiftDefinition { get; set; }

    public bool IsRestDay { get; set; }
}

/// <summary>Remarks8 Item 6 — Pattern يوم/أيام الراحة الأسبوعية للموظف (Bit Mask فوق DayOfWeek:
/// bit 0 = الأحد ... bit 6 = السبت). مصدر افتراضي للمولّد الجماعي (Item 7) بس — مش بيتقرأ من
/// Attendance/LeaveDaysCalculator مباشرة؛ الحقيقة المُجسَّدة تفضل دايمًا صفوف ShiftSchedule نفسها.</summary>
public class EmployeeWeeklyRestDays : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int WeeklyRestDaysMask { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>
/// مصدر الحقيقة الوحيد للحضور (قاعدة 10) — مابيتعدّلش ولا بيتمسح، التصحيح بصف جديد
/// (<see cref="IsCorrection"/>/<see cref="CorrectsTimeEntryId"/>). <see cref="Status"/> نفسها
/// قابلة للتغيير (قبول/رفض مقترح) — ده انتقال حالة على نفس الصف، مش تعديل للبيانات الزمنية نفسها.
/// <see cref="DeviceId"/> — Phase 3B: FK حقيقي على AttendanceDevices(Id) دلوقتي (Migration، مش عمود جديد).
/// </summary>
public class TimeEntry : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public TimeEntryType EntryType { get; set; }
    public DateTime TimestampUtc { get; set; }
    public TimeEntrySource Source { get; set; }

    /// <summary>قاعدة 11 — التلقيم من POS.Shift (نداء مباشر، Phase-3-Research.md §3.1، مش Event).</summary>
    public long? POSShiftId { get; set; }
    public Shift? POSShift { get; set; }

    /// <summary>Phase 3B — بصمة من جهاز (Source = Device). FK على AttendanceDevices(Id).</summary>
    public long? DeviceId { get; set; }
    public AttendanceDevice? Device { get; set; }

    public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Accepted;

    public bool IsCorrection { get; set; }
    public long? CorrectsTimeEntryId { get; set; }
    public TimeEntry? CorrectsTimeEntry { get; set; }
}

/// <summary>الملخص اليومي المحسوب من TimeEntry المقبولة (قاعدة 14) — الوحيد اللي بيدخل الرواتب لاحقًا.</summary>
public class Attendance : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public long? ShiftScheduleId { get; set; }
    public ShiftSchedule? ShiftSchedule { get; set; }

    public DateTime? FirstInUtc { get; set; }
    public DateTime? LastOutUtc { get; set; }
    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public AttendanceStatus Status { get; set; }
    public bool IsApproved { get; set; }
}
