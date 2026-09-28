using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Organization;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Company-scoped organizational unit (branch or admin department) — hierarchical via
/// <see cref="ParentId"/> (Docs/Modules/10-Module-HR-Payroll.md §2.1). Cycle prevention on
/// <see cref="ParentId"/> is enforced by <see cref="OrgUnitHierarchy"/>, not a DB constraint.
/// </summary>
public class OrgUnit : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public long? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }

    public long? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Plain column, no FK yet — Employee does not exist until Batch B2 (Docs/Implementation/HR-Core-Plan.md §1.1).</summary>
    public long? ManagerEmployeeId { get; set; }

    public long? CostCenterDimensionValueId { get; set; }
    public CostCenterDimensionValue? CostCenterDimensionValue { get; set; }
}
