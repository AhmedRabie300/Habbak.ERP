using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// إعداد نموذج الإنتاج والبيع (02-Module-Inventory-Manufacturing.md, section 2.7) — screen #21.
/// ScopeId has no FK/navigation: Branch and Item scopes reference entities already in this
/// codebase (validated by id where the command needs to), but POS scope references an entity from
/// 05-Module-POS-Shifts.md, not built yet — same deferral already applied to CustodyOfficer.EmployeeId
/// pending the HR module.
/// </summary>
public class ProductionSalesModeSetting : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public SettingScopeType ScopeType { get; set; }

    /// <summary>Null only when ScopeType = Company (module doc's own field-table note).</summary>
    public long? ScopeId { get; set; }

    public ProductionSalesMode Mode { get; set; }
}
