namespace Habbak.ERP.Domain.Common;

public abstract class AuditableEntity : IAuditableEntity
{
    public long Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public long CreatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public long UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public long? DeletedBy { get; set; }
}
