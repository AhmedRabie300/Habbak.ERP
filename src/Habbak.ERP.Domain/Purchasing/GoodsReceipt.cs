using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>Section 6.5's state machine — automatic-document rule doesn't apply here (this is a
/// manual document, unlike the Inventory module's InventoryAdjustment): Draft → Posted moves stock.</summary>
public enum GoodsReceiptStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3,
    Archived = 4
}

public enum GoodsReceiptQualityCheckStatus
{
    Pending = 1,
    Passed = 2,
    PartiallyPassed = 3,
    Failed = 4
}

/// <summary>
/// إذن إضافة / استلام مشتريات (03-Module-Purchasing.md, section 4.6) — screen #6.
///
/// A receipt is raised against exactly one source: either a Confirmed/PartiallyReceived
/// <see cref="PurchaseOrder"/>, or — in the "invoice only" purchasing cycle, where no order is
/// raised at all — the <see cref="PurchaseInvoice"/> itself. Both references are nullable but one
/// of them must be set; that invariant lives in CreateGoodsReceiptCommand's validator rather than
/// the schema, since a database CHECK cannot be expressed through EF here.
///
/// An earlier pass required PurchaseOrderId outright, which made the invoice-only cycle impossible
/// to complete: goods bought on a direct invoice could be billed but never received, so stock had
/// to be moved through an unrelated Inventory StockIn that carried no supplier or invoice link.
/// </summary>
public class GoodsReceipt : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string ReceiptNumber { get; set; } = null!;
    public DateOnly ReceiptDate { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public long? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public long? PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public string? Notes { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

public class GoodsReceiptLine : AuditableEntity
{
    public long GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>الكمية المستلمة فعلاً — لازم تساوي AcceptedQuantity + RejectedQuantity.</summary>
    public decimal Quantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }

    /// <summary>إلزامي لو RejectedQuantity &gt; 0.</summary>
    public string? RejectedReason { get; set; }

    /// <summary>مخزن التالف/المرتجعات — إلزامي لو RejectedQuantity &gt; 0.</summary>
    public long? RejectedWarehouseId { get; set; }
    public Warehouse? RejectedWarehouse { get; set; }

    public decimal UnitCost { get; set; }

    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    /// <summary>
    /// Base units in one UnitId (1 for the item's base unit), copied when the line is saved so a
    /// later change to the item's conversions does not rewrite this document (Fixes-Batch-2026-09-19).
    /// </summary>
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>The received quantity (accepted and rejected convert the same way) in the item's base unit: Quantity × UnitFactor — what stock moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>UnitCost per base unit: ÷ UnitFactor — what stock is valued at.</summary>
    public decimal BaseUnitCost { get; set; }

    /// <summary>الكمية المتبقية على أمر الشراء وقت إنشاء هذا الاستلام (PurchaseOrderLine.Quantity −
    /// PurchaseOrderLine.ReceivedQuantity) — مش الكمية الأصلية للأمر بالكامل، عشان الاستلامات
    /// الجزئية المتكررة يكون الفرق فيها معنى صحيح في كل مرة.</summary>
    public decimal? ExpectedQuantity { get; set; }
    public decimal? VarianceQuantity { get; set; }

    /// <summary>إلزامي لو VarianceQuantity ≠ 0.</summary>
    public string? VarianceReason { get; set; }

    /// <summary>إلزامي لو Item.IsTracked = true.</summary>
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public GoodsReceiptQualityCheckStatus QualityCheckStatus { get; set; } = GoodsReceiptQualityCheckStatus.Pending;
    public string? QualityCheckNotes { get; set; }
}
