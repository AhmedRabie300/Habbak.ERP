using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

public enum PurchaseReturnReason
{
    Damaged = 1,
    Expired = 2,
    WrongItem = 3,
    WrongQuantity = 4,
    PriceMismatch = 5,
    QualityIssue = 6,
    Other = 7
}

public enum PurchaseReturnStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3,
    Archived = 4
}

/// <summary>
/// مردود مشتريات (03-Module-Purchasing.md, section 4.7) — screen #7. Adds one field the doc's own
/// table never lists: WarehouseId (required) — rule 7 says posting "يُقلل المخزون" (decreases
/// stock) but no warehouse field exists to decrease FROM; the same spec inconsistency already noted
/// on PurchaseInvoice (AllowInvoiceWithoutReceipt). No JournalEntryId is ever set — rule 7's
/// "قيداً محاسبياً عكسياً" is deferred like every other Purchasing document (IPostingService isn't
/// wired in here either).
/// </summary>
public class PurchaseReturn : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string ReturnNumber { get; set; } = null!;
    public DateOnly ReturnDate { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public long? PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    /// <summary>المخزن اللي هيتخصم منه المردود — عادة نفس مخزن الاستلام، أو مخزن التالف
    /// (WarehouseType.DamagedReturns) لو السبب Damaged/Expired/QualityIssue.</summary>
    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public PurchaseReturnReason Reason { get; set; }
    public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Draft;

    public string? Notes { get; set; }

    /// <summary>The entry the posting engine made for this document; null when its screen had no active template.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<PurchaseReturnLine> Lines { get; set; } = new List<PurchaseReturnLine>();
}

public class PurchaseReturnLine : AuditableEntity
{
    public long PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>
    /// بند الفاتورة اللي المردود ده راجع عليه (Remarks4, item 8). إلزامي لأي مردود مرتبط بفاتورة:
    /// الكمية المردودة متسقفة بكمية البند دي ناقص اللي اترد قبل كده، والصنف بييجي من البند نفسه
    /// فمستحيل يترد صنف مش في الفاتورة. بيفضل null للمردودات القديمة اللي الترحيل ماقدرش يربطها،
    /// وللمردود اللي من غير فاتورة أصلًا.
    /// </summary>
    public long? PurchaseInvoiceLineId { get; set; }
    public PurchaseInvoiceLine? PurchaseInvoiceLine { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    /// <summary>
    /// Base units in one UnitId (1 for the item's base unit), copied when the line is saved so a
    /// later change to the item's conversions does not rewrite this document (Fixes-Batch-2026-09-19).
    /// </summary>
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>The returned quantity in the item's base unit: Quantity × UnitFactor — what stock moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>UnitCost per base unit: ÷ UnitFactor — what stock is valued at.</summary>
    public decimal BaseUnitCost { get; set; }

    /// <summary>إلزامي لو Item.IsTracked=true (نفس قاعدة 8 المشتركة مع كل حركة مخزون).</summary>
    public string? BatchNumber { get; set; }
}
