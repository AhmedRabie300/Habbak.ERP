using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>Company-scoped — ID card, health certificate, criminal record, qualification... (Docs/Modules/10-Module-HR-Payroll.md §2.1).</summary>
public class EmployeeDocumentType : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public bool RequiresExpiry { get; set; }
    public bool IsMandatory { get; set; }
    public int? ExpiryAlertDays { get; set; }
}
