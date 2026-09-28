namespace Habbak.ERP.Domain.Attendance;

// Docs/Modules/10-Module-HR-Payroll.md §2.2 + §2.6, قواعد 10-27. HrRequestStatus (مشترك مع
// LeaveRequest/OvertimeRequest، وPhase 5 لاحقًا) عمدًا في HR/Enums.cs مش هنا (Phase-3-Research.md §3.5)
// — نفس الفكرة، LeaveDayCountingMode في HR/HrSettings.cs جنب HrMonthBasis.

public enum TimeEntryType { In = 1, Out = 2 }

/// <summary>قاعدة 10-11 — Device مش من نطاق Phase 3 (Phase 3B)، موجودة هنا كقيمة Enum بس زي ما
/// `10-Module-HR-Payroll.md §2.6` بتوثّق.</summary>
public enum TimeEntrySource { Manual = 1, SelfService = 2, Kiosk = 3, POSShift = 4, Device = 5 }

/// <summary>قاعدة 11 — المقترح من POS بيتقبل تلقائيًا لو مفيش تعارض، وغير كده بيستنى المشرف.</summary>
public enum TimeEntryStatus { Suggested = 1, Accepted = 2, Dismissed = 3 }

public enum AttendanceStatus { Present = 1, Absent = 2, Leave = 3, Holiday = 4, RestDay = 5 }

public enum LeaveAccrualMethod { Monthly = 1, Annual = 2, Hourly = 3, None = 4 }

/// <summary>قاعدة 26 — الحركات النهائية بس؛ "Pending" (حجز مؤقت على LeaveBalance) عمدًا مش هنا
/// (Phase-3-Research.md §3.6).</summary>
public enum LeaveBalanceMovementType { Accrual = 1, Usage = 2, Reversal = 3, CarryOver = 4, Expiry = 5, CashOut = 6, Adjustment = 7 }

/// <summary>قاعدة 18 — القيم الابتدائية القانونية في `10-Module-HR-Payroll.md §2.4` (OvertimeRate، Phase 4).</summary>
public enum OvertimeType { Day = 1, Night = 2, RestDay = 3, PublicHoliday = 4 }

// Phase 3B (Fingerprint devices) — Phase-3B-Research.md §3.

/// <summary>مصدر البصمة الخام — نفس الـEnum مستخدَم في RawPunch وAttendanceDeviceLog.</summary>
public enum RawPunchSourceType { Push = 1, FileImport = 2 }

/// <summary>Phase-3B-Research.md §3.1 — الخام مابيتعدّلش أبدًا، العمودين دول بس هما اللي بيتحدّثوا بعد الإدخال.</summary>
public enum RawPunchProcessingStatus { Pending = 1, Processed = 2, Skipped = 3 }

/// <summary>سبب تخطّي بصمة خام (§4 بند 2) — بيفضل قابل لإعادة المحاولة (مش Terminal) عدا DuplicatePunch
/// (اللي أصلًا مابيتسجلش كصف Pending من الأساس، Unique Index بيمنعه عند الإدخال، §3.1).</summary>
public enum RawPunchSkipReason { NoEmployeeMapping = 1, DeviceNotRegistered = 2, DuplicatePunch = 3 }

public enum AttendanceDeviceLogStatus { Success = 1, PartialFailure = 2, Failed = 3 }
