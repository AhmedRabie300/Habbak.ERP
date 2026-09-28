using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Docs/Modules/10-Module-HR-Payroll.md §2.1, Batch B3. Reuses the existing generic Attachment table
/// (Common/Attachment.cs) via a direct scalar FK (AttachmentId), not the polymorphic
/// EntityType/EntityId lookup Attachment normally uses elsewhere — the request is explicit that this
/// is a 1:1 document-to-file link, so the plain FK is simpler and still queryable. Same
/// IEmployeeScopedEntity/IBranchScopedEntity notes as EmploymentContract.
/// </summary>
public class EmployeeDocument : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long EmployeeDocumentTypeId { get; set; }
    public EmployeeDocumentType EmployeeDocumentType { get; set; } = null!;

    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public long AttachmentId { get; set; }
    public Attachment Attachment { get; set; } = null!;

    public string? DocumentNumber { get; set; }
}
