namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// Audit entries older than their company's SystemSettings.AuditRetentionYears, moved out of the
/// working log by the daily maintenance job. Same columns, same Id — kept, never deleted, and as
/// append-only as the log itself.
/// </summary>
public class AuditLogArchive
{
    public long Id { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public long UserId { get; set; }
    public AuditActionType ActionType { get; set; }
    public string EntityType { get; set; } = null!;
    public long? EntityId { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? AdditionalData { get; set; }
    public DateTime ArchivedAtUtc { get; set; }
}
