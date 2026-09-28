using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Domain.Attendance;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §3.0/§7 — جهاز بصمة فعلي (ZKTeco أو غيره). Lookup
/// عادي زي WorkShiftDefinition، بس مع حقول بروتوكول إضافية (<see cref="SerialNumber"/> اللي الجهاز
/// بيبعته وقت الـPush، <see cref="DeviceSecretHash"/> Hash فقط زي كلمة المرور — القرار الفرعي 4).
/// </summary>
public class AttendanceDevice : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    /// <summary>نص حر، مش Enum — تنوع موديلات الأجهزة كبير ("ZKTeco K40" وهكذا).</summary>
    public string? Model { get; set; }

    /// <summary>الـSN اللي الجهاز بيبعته في بروتوكول الـPush (ADMS/iClock) — مفتاح المطابقة الوحيد
    /// وقت استقبال بصمة (القرار الفرعي 1)، مش <see cref="AuditableEntity.Id"/>.</summary>
    public string? SerialNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Hash فقط (نفس IPasswordHasher المستخدَم لكلمات مرور المستخدمين) — السر الخام
    /// بيتولّد وقت التسجيل/إعادة التوليد وبيتعرض مرة واحدة بس، مش متخزّن.</summary>
    public string DeviceSecretHash { get; set; } = null!;

    /// <summary>آخر Push/Import ناجح — يغذي شاشة سجل الأجهزة (3B.6).</summary>
    public DateTime? LastSeenAtUtc { get; set; }
}

/// <summary>سجل كل محاولة Sync (Push أو File Import) لجهاز معيّن — شاشة عرض بس (3B.6).</summary>
public class AttendanceDeviceLog : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;

    public RawPunchSourceType SyncType { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public AttendanceDeviceLogStatus Status { get; set; }
    public int PunchesReceived { get; set; }
    public int PunchesProcessed { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// ربط موظف بجهاز بصمة — <see cref="DeviceUserId"/> رقم المستخدم على الجهاز نفسه، لا علاقة له
/// بـ <see cref="Employee.UserId"/> (حساب الدخول في النظام). فريد على (<see cref="AttendanceDeviceId"/>,
/// <see cref="DeviceUserId"/>) — نفس رقم المستخدم على نفس الجهاز ميتكررش لموظفين مختلفين. موظف واحد
/// ممكن يكون ليه أكتر من صف (أكتر من جهاز/فرع).
/// </summary>
public class EmployeeDeviceMapping : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;

    public string DeviceUserId { get; set; } = null!;
}

/// <summary>
/// البصمة الخام كما وصلت فعليًا — خام ومابيتعدّلش أبدًا بعد الإدخال (نفس مبدأ TimeEntry، "التصحيح
/// بصف جديد"). عمود <see cref="ProcessingStatus"/>/<see cref="ResultTimeEntryId"/>/<see cref="SkipReason"/>
/// بس هما اللي بيتحدّثوا بعد كده (AttendanceDeviceJobs، §4). Deduplication عند الإدخال نفسه —
/// Unique Index على (AttendanceDeviceId, DeviceUserId, PunchTimestampUtc)، §3.1.
/// </summary>
public class RawPunch : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long AttendanceDeviceId { get; set; }
    public AttendanceDevice AttendanceDevice { get; set; } = null!;

    /// <summary>خام من الجهاز، مش FK — بيترجم لـEmployeeId عن طريق EmployeeDeviceMapping وقت المعالجة.</summary>
    public string DeviceUserId { get; set; } = null!;

    public DateTime PunchTimestampUtc { get; set; }

    /// <summary>In/Out/Break.. زي ما الجهاز بعتها، خام (القرار الفرعي 2).</summary>
    public int? RawStatus { get; set; }

    /// <summary>بصمة/بطاقة/وش.. خام، للتوثيق بس.</summary>
    public int? RawVerifyType { get; set; }

    public RawPunchSourceType SourceType { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public RawPunchProcessingStatus ProcessingStatus { get; set; } = RawPunchProcessingStatus.Pending;
    public DateTime? ProcessedAtUtc { get; set; }

    public long? ResultTimeEntryId { get; set; }
    public TimeEntry? ResultTimeEntry { get; set; }

    public RawPunchSkipReason? SkipReason { get; set; }
}
