using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Settings;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Docs/Modules/10-Module-HR-Payroll.md §2.1, §4.1 — Batch B2. <see cref="ManagerId"/> is a
/// management-reporting reference, not the entity's own hierarchy (deliberately not ParentId, rule
/// §2.1): ApprovalApproverType.DirectManager resolves it at approval-engine runtime, which does not
/// exist yet (Docs/Implementation/HR-Core-Plan.md, risk 6) — <see cref="Status"/> carries a
/// <c>Rejected</c> value for schema completeness only, same as PurchaseOrder/Shift today
/// (Phase-1.1-Research.md §2.4); nothing calls the approval service from here yet.
/// <see cref="BranchId"/> is required at the database level (.IsRequired() in configuration) but
/// typed nullable in C# to satisfy IBranchScopedEntity, mirroring Shift/POSTerminal.
/// </summary>
public class Employee : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public long OrgUnitId { get; set; }
    public OrgUnit OrgUnit { get; set; } = null!;

    public long JobPositionId { get; set; }
    public JobPosition JobPosition { get; set; } = null!;

    public long JobGradeId { get; set; }
    public JobGrade JobGrade { get; set; } = null!;

    /// <summary>Cycle-checked the same way as OrgUnit.ParentId, via the same OrgUnitHierarchy helper (rule 3).</summary>
    public long? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    /// <summary>The official link to the login account (rule 1) — unique within the company (filtered index).</summary>
    public long? UserId { get; set; }
    public User? User { get; set; }

    public DateOnly HireDate { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Draft;

    /// <summary>Overrides the branch's own cost center for management-level employees.</summary>
    public long? CostCenterDimensionValueId { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public EmployeePersonalData? PersonalData { get; set; }
}
