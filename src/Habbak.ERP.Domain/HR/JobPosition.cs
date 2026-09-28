using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>Company-scoped — cashier, barista, shift supervisor... (Docs/Modules/10-Module-HR-Payroll.md §2.1).</summary>
public class JobPosition : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public long? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }

    public long? DefaultJobGradeId { get; set; }
    public JobGrade? DefaultJobGrade { get; set; }

    /// <summary>Plain column, no FK — SalaryStructure belongs to the future Payroll module (Docs/Modules/10-Module-HR-Payroll.md §5.2).</summary>
    public long? DefaultSalaryStructureId { get; set; }
}
