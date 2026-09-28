namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Dtos;

/// <summary>مفيش DeviceSecretHash خالص — السر الخام مابيتخزنش، ومفيش داعي نرجّع الـHash نفسه.</summary>
public sealed class AttendanceDeviceDto
{
    public required long Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public long? BranchId { get; init; }
    public required bool IsActive { get; init; }
    public DateTime? LastSeenAtUtc { get; init; }
}

/// <summary>الرد على Create/RegenerateSecret بس — السر الخام بيتعرض مرة واحدة، مش متخزّن أصلًا.</summary>
public sealed record AttendanceDeviceSecretDto(long Id, string DeviceSecret);

public sealed class AttendanceDeviceLogDto
{
    public required long Id { get; init; }
    public required long AttendanceDeviceId { get; init; }
    public required string DeviceCode { get; init; }
    public required int SyncType { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public DateTime? FinishedAtUtc { get; init; }
    public required int Status { get; init; }
    public required int PunchesReceived { get; init; }
    public required int PunchesProcessed { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>§5 — Reconciliation. تجميع حسب SkipReason/ProcessingStatus + قائمة الاستثناءات.</summary>
public sealed class RawPunchReconciliationSummaryDto
{
    public required int TotalReceived { get; init; }
    public required int Processed { get; init; }
    public required int Pending { get; init; }
    public required int SkippedNoEmployeeMapping { get; init; }
    public required int SkippedOther { get; init; }
}

public sealed class RawPunchExceptionDto
{
    public required long Id { get; init; }
    public required long AttendanceDeviceId { get; init; }
    public required string DeviceCode { get; init; }
    public required string DeviceUserId { get; init; }
    public required DateTime PunchTimestampUtc { get; init; }
    public required string ProcessingStatus { get; init; }
    public string? SkipReason { get; init; }
}
