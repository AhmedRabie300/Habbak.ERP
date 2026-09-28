using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// قيمة البُعد — supports hierarchy for dimensions that are naturally hierarchical (e.g. Cost
/// Center); ParentId/Level stay null/0 for flat dimensions (Sales Channel, Cashier)
/// (01-Module-Accounting.md, section 2.1).
/// </summary>
public class CostCenterDimensionValue : AuditableEntity
{
    public long CostCenterDimensionId { get; set; }
    public CostCenterDimension CostCenterDimension { get; set; } = default!;

    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;

    /// <summary>Used only for hierarchical dimensions; always null for flat ones.</summary>
    public long? ParentId { get; set; }
    public CostCenterDimensionValue? Parent { get; set; }

    /// <summary>Computed from ParentId; 0 for non-hierarchical dimensions.</summary>
    public int Level { get; set; }

    /// <summary>For a Branch-linked dimension this mirrors Branch.IsActive; for a manually-entered
    /// dimension it is a plain on/off toggle. Inactive values are hidden from selection everywhere
    /// but stay in place so existing journal lines that already reference them remain valid.</summary>
    public bool IsActive { get; set; } = true;
}
