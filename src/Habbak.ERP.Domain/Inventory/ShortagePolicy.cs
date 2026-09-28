using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>سياسة تجاوز الحدود والنقص (02-Module-Inventory-Manufacturing.md, section 2.7) —
/// screen #22. One row per company (rule 33: OverrideShortage is the single unified permission
/// this policy gates for both the negative-balance override, rule 1, and the branch-request-limit
/// override, rule 4).</summary>
public class ShortagePolicy : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public bool AllowOverrideOnShortage { get; set; }
    public bool RequiresApprovalForOverride { get; set; }
}

/// <summary>إعدادات المخزون العامة (section 2.7) — screen #23. One row per company.</summary>
public class InventorySettings : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    /// <summary>Rule 36 — default 90, never hard-coded.</summary>
    public int SlowMovingThresholdDays { get; set; } = 90;
}
