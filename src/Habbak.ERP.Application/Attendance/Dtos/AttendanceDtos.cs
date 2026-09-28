using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Application.Attendance.Dtos;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 3, Sub-Batch 3.3 — كل الـ DTOs في ملف واحد
// (نفس فكرة تجميع الملفات المتّبعة في الـ Commands/Queries لتقليل عدد الملفات).

public sealed class WorkShiftDefinitionDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }
    public required int BreakMinutes { get; init; }
    public required bool IsNightShift { get; init; }
}

public sealed class ShiftScheduleDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required long? BranchId { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public long? WorkShiftDefinitionId { get; init; }
    public required bool IsRestDay { get; init; }
}

public sealed class TimeEntryDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required TimeEntryType EntryType { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required TimeEntrySource Source { get; init; }
    public long? POSShiftId { get; init; }
    public long? DeviceId { get; init; }
    public required TimeEntryStatus Status { get; init; }
    public required bool IsCorrection { get; init; }
    public long? CorrectsTimeEntryId { get; init; }
}

public sealed class AttendanceDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required DateOnly WorkDate { get; init; }
    public long? ShiftScheduleId { get; init; }
    public DateTime? FirstInUtc { get; init; }
    public DateTime? LastOutUtc { get; init; }
    public required int WorkedMinutes { get; init; }
    public required int LateMinutes { get; init; }
    public required int EarlyLeaveMinutes { get; init; }
    public required int OvertimeMinutes { get; init; }
    public required AttendanceStatus Status { get; init; }
    public required bool IsApproved { get; init; }
}

public sealed class EmployeeAttendanceReportLineDto
{
    public required DateOnly WorkDate { get; init; }
    public required AttendanceStatus Status { get; init; }
    public DateTime? FirstInUtc { get; init; }
    public DateTime? LastOutUtc { get; init; }
    public required int WorkedMinutes { get; init; }
    public required int LateMinutes { get; init; }
    public required int EarlyLeaveMinutes { get; init; }
    public required int OvertimeMinutes { get; init; }
    public required bool IsApproved { get; init; }
}

public sealed class AttendanceConflictDto
{
    public required long EmployeeId { get; init; }
    public required string EmployeeCode { get; init; }
    public required string EmployeeNameAr { get; init; }
    public required DateOnly WorkDate { get; init; }
    public required long POSShiftId { get; init; }
    public required string Description { get; init; }
}

public sealed class LeaveTypeDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required LeaveAccrualMethod AccrualMethod { get; init; }
    public required int AnnualDays { get; init; }
    public required int MaxCarryOver { get; init; }
    public required bool IsPaid { get; init; }
    public required decimal PaidPercentage { get; init; }
    public long? DeductFromLeaveTypeId { get; init; }
    public required bool RequiresDocument { get; init; }
    public int? MaxDaysPerRequest { get; init; }
    public Gender? GenderRestriction { get; init; }
    public int? MaxTimesInService { get; init; }
    public required bool IsCashableOnTermination { get; init; }
}

public sealed class LeaveBalanceDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required long LeaveTypeId { get; init; }
    public required int Year { get; init; }
    public required decimal AccruedThisYear { get; init; }
    public required decimal CarriedOver { get; init; }
    public required decimal Used { get; init; }
    public required decimal Pending { get; init; }
    public required decimal Available { get; init; }
}

public sealed class LeaveBalanceHistoryDto
{
    public required long Id { get; init; }
    public required LeaveBalanceMovementType MovementType { get; init; }
    public required decimal Days { get; init; }
    public required DateOnly EffectiveDate { get; init; }
    public string? SourceType { get; init; }
    public long? SourceId { get; init; }
    public string? Reason { get; init; }
}

public sealed class LeaveRequestDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required long LeaveTypeId { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required decimal Days { get; init; }
    public string? Reason { get; init; }
    public required HrRequestStatus Status { get; init; }
    public long? ApprovalInstanceId { get; init; }
}

public sealed class CalculateLeaveDaysResultDto
{
    public required decimal Days { get; init; }
    public required LeaveDayCountingMode Mode { get; init; }
    public required bool UsedFallback { get; init; }
}

public sealed class HolidayDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required bool IsActive { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public required int Year { get; init; }
    public required bool IsNational { get; init; }
    public long? BranchId { get; init; }
}

public sealed class OvertimeRequestDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required DateOnly WorkDate { get; init; }
    public required int PlannedMinutes { get; init; }
    public required OvertimeType OvertimeType { get; init; }
    public string? Reason { get; init; }
    public required HrRequestStatus Status { get; init; }
    public long? ApprovalInstanceId { get; init; }
    public int? ActualMinutes { get; init; }
}
