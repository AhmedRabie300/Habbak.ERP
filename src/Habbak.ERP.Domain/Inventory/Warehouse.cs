using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>المخزن (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
public class Warehouse : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    /// <summary>عمود مباشر Nullable — null للمخازن المركزية (WarehouseType = Main)، ومحدد لمخازن
    /// الفروع (قاعدة 24).</summary>
    public long? BranchId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public WarehouseType WarehouseType { get; set; }

    /// <summary>Per-warehouse override of rule 1 (no movement may take a balance negative) —
    /// finer-grained than the company-wide ShortagePolicy.AllowOverrideOnShortage (section 2.7,
    /// not yet built); IStockMovementService checks this flag directly.</summary>
    public bool AllowNegativeBalance { get; set; }

    public bool IsActive { get; set; } = true;
}
