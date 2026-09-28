namespace Habbak.ERP.Domain.Common;

/// <summary>
/// My Remarks/Remarks2.md, remark 3.3 — a single generic attachment mechanism reusable by any
/// document/master-data screen (polymorphic EntityType/EntityId, same convention as
/// StockTransaction.SourceDocumentType), rather than a parallel attachments table per module.
/// Content is stored directly in SQL Server (varbinary(max)) — this app's attachment volumes
/// (invoice scans, receipts) are small enough that a filesystem/blob-storage layer would be
/// premature infrastructure for what this remark actually asks for.
/// </summary>
public class Attachment : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public string EntityType { get; set; } = null!;
    public long EntityId { get; set; }

    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public byte[] Content { get; set; } = null!;
}
