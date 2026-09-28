using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>بند المستند المخزني (02-Module-Inventory-Manufacturing.md, section 2.3).</summary>
public class WarehouseDocumentLine : AuditableEntity
{
    public long WarehouseDocumentId { get; set; }
    public WarehouseDocument WarehouseDocument { get; set; } = null!;

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>
    /// The unit the quantity (and the unit cost) is in — the item's base unit or one of its ItemUnitConversion units
    /// (Remarks3). <see cref="UnitFactor"/> is how many base units one of it holds, copied when the
    /// line is saved so a later change to the item's conversions does not rewrite this document.
    /// </summary>
    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>إلزامي لو Item.IsTracked = true — يُفحص وقت الترحيل عبر IStockMovementService (قاعدة 8).</summary>
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>تُستخدم حصريًا في TransferReceipt (غير مُفعَّل بعد في هذه الدفعة).</summary>
    public decimal? ExpectedQuantity { get; set; }
    public decimal? VarianceQuantity { get; set; }
}
