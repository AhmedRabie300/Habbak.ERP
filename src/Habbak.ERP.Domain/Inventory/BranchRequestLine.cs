using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>بند طلب توريد الفرع (02-Module-Inventory-Manufacturing.md, section 2.4).</summary>
public class BranchRequestLine : AuditableEntity
{
    public long BranchRequestId { get; set; }
    public BranchRequest BranchRequest { get; set; } = null!;

    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    /// <summary>يُتحقَّق مقابل BranchItemLimit وقت تقديم الطلب (قاعدة 4).</summary>
    public decimal RequestedQuantity { get; set; }

    /// <summary>قد تقل عن المطلوب دون رفض كامل الطلب (قاعدة 6).</summary>
    public decimal? ApprovedQuantity { get; set; }

    /// <summary>
    /// The unit the requested and approved quantity is in — the item's base unit or one of its ItemUnitConversion units
    /// (Remarks3). <see cref="UnitFactor"/> is how many base units one of it holds, copied when the
    /// line is saved so a later change to the item's conversions does not rewrite this document.
    /// </summary>
    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public decimal UnitFactor { get; set; } = 1;
}
