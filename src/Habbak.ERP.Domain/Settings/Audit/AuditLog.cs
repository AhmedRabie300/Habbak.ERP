namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// Who did what. Deliberately not an AuditableEntity: a row is written once and never updated or
/// deleted (AppDbContext refuses both), and archived after SystemSettings.AuditRetentionYears.
/// Sensitive values never reach it in clear text — build field changes with <see cref="FieldChange"/>.
/// </summary>
public class AuditLog
{
    public const string Redacted = "[REDACTED]";

    public long Id { get; set; }
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    /// <summary>0 = the system itself (startup seeding, background work) — which is why there is no FK to User.</summary>
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

    /// <summary>JSON.</summary>
    public string? AdditionalData { get; set; }

    /// <summary>A field change, with both values replaced by <see cref="Redacted"/> when the field is in <see cref="FieldPermissionCatalog"/>.</summary>
    public static AuditLog FieldChange(
        long? companyId, long userId, string entityType, long entityId, string fieldName,
        string? oldValue, string? newValue, DateTime occurredAtUtc)
    {
        var sensitive = FieldPermissionCatalog.IsSensitive(entityType, fieldName);
        return new AuditLog
        {
            CompanyId = companyId,
            UserId = userId,
            ActionType = AuditActionType.Update,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            OldValue = sensitive && oldValue is not null ? Redacted : oldValue,
            NewValue = sensitive && newValue is not null ? Redacted : newValue,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
