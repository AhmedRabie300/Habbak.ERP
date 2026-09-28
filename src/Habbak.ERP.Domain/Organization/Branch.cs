using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Organization;

/// <summary>
/// فرع — company-wide master data, standalone from the "Branch" cost-center dimension: this table
/// is that dimension's live value source when a CostCenterDimension.LinkedEntityType is Branch
/// (only active branches are offered as selectable values there).
/// </summary>
public class Branch : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
