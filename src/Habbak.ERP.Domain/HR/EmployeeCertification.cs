using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Docs/Modules/10-Module-HR-Payroll.md §2.1, Batch B3 ("باريستا، وسلامة غذاء" — barista/food-safety
/// certifications). Same IEmployeeScopedEntity/IBranchScopedEntity notes as EmploymentContract.
/// </summary>
public class EmployeeCertification : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? CertificateNumber { get; set; }

    /// <summary>Phase 3C, Remarks8 item 3 — same single-scalar-FK pattern as EmployeeDocument.AttachmentId,
    /// nullable for the same reason as EmploymentContract.AttachmentId (set later via
    /// SetEmployeeCertificationAttachmentCommand, not at creation).</summary>
    public long? AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }
}
