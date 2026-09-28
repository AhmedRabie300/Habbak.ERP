using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// عملية جرد (02-Module-Inventory-Manufacturing.md, section 2.5) — screen #14's multi-stage flow:
/// Create (this entity + its lines) → Counters (CountedQuantity entry while InProgress) →
/// interactive settlement (per-line accept/reject while PendingSettlement) → Close (auto-posts the
/// approved variances as an InventoryAdjustment WarehouseDocument, rule 11).
/// </summary>
public class InventoryCount : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string CountNumber { get; set; } = null!;
    public DateOnly CountDate { get; set; }

    public InventoryCountType CountType { get; set; }
    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.Draft;
    public long? ApprovalInstanceId { get; set; }

    /// <summary>The entry for the count differences, made when the count is closed.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<InventoryCountLine> Lines { get; set; } = new List<InventoryCountLine>();
}

public class InventoryCountLine : AuditableEntity
{
    public long InventoryCountId { get; set; }
    public InventoryCount? InventoryCount { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>Snapshot of StockBalance.QuantityOnHand at count creation time — never touched
    /// afterward, even if other movements change the real balance while the count is in progress.</summary>
    public decimal SystemQuantity { get; set; }

    public decimal? CountedQuantity { get; set; }

    /// <summary>
    /// The unit the counted quantity (SystemQuantity and VarianceQuantity stay in the base unit) is in — the item's base unit or one of its ItemUnitConversion units
    /// (Remarks3). <see cref="UnitFactor"/> is how many base units one of it holds, copied when the
    /// line is saved so a later change to the item's conversions does not rewrite this document.
    /// </summary>
    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>Computed as CountedQuantity - SystemQuantity the moment CountedQuantity is entered.</summary>
    public decimal? VarianceQuantity { get; set; }

    public SettlementDecision SettlementDecision { get; set; } = SettlementDecision.Pending;

    /// <summary>Required once VarianceQuantity != 0, regardless of the settlement decision made
    /// (rule's own wording ties it to the variance existing, not to which decision was taken).</summary>
    public string? SettlementReason { get; set; }
}
