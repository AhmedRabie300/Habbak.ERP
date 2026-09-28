namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Base contract for every entity in the system (00-Project-Overview.md, section 8.4).
/// </summary>
public interface IAuditableEntity
{
    long Id { get; set; }
    byte[] RowVersion { get; set; }
    DateTime CreatedAtUtc { get; set; }
    long CreatedBy { get; set; }
    DateTime UpdatedAtUtc { get; set; }
    long UpdatedBy { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    long? DeletedBy { get; set; }
}
