using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>Shared between PurchaseInvoice and PurchaseExpense (section 4.5/4.8) — how a
/// header-level cost total gets spread across line items.</summary>
public enum CostAllocationMethod
{
    ByValue = 1,
    ByQuantity = 2,
    ByWeight = 3,
    Manual = 4
}

/// <summary>
/// Section 6.4's state machine. Draft → PendingApproval → Posted, then PartiallyPaid/Paid once
/// AmountPaid accumulates via a posted supplier-payment Voucher (screen #9) — PendingPayment stays
/// unreachable (redundant with Posted) and Overdue stays unreachable (would need a scheduled job
/// this codebase has none of), same forward-declared-enum-value pattern already used for
/// PurchaseRequestStatus.Converted and PurchaseOrderStatus.PartiallyReceived.
/// </summary>
public enum PurchaseInvoiceStatus
{
    Draft = 1,
    PendingApproval = 2,
    Posted = 3,
    PendingPayment = 4,
    PartiallyPaid = 5,
    Paid = 6,
    Overdue = 7,
    Cancelled = 8,
    Rejected = 9,
    Archived = 10
}

/// <summary>
/// فاتورة شراء (03-Module-Purchasing.md, section 4.5) — screen #5. Deviates from the doc's field
/// table in two places, both deferred pending modules that don't exist yet: no ETAStatus (06-Module-ETA-Compliance.md isn't built), and no per-line
/// LineCurrencyCode/LineExchangeRate/LineAmountInBaseCurrency (a line billed in a different currency
/// than its own invoice is a rare edge case not worth the complexity here — the whole invoice
/// shares one CurrencyCode/ExchangeRate, same simplification JournalEntry's own header-level
/// currency already makes in 01-Module-Accounting.md).
/// </summary>
public class PurchaseInvoice : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string InvoiceNumber { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string? SupplierInvoiceNumber { get; set; }

    public long? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public long? GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }

    /// <summary>
    /// Where the goods on this invoice are to be received. Only consulted when
    /// PurchaseCycleSettings.AutoCreateReceiptOnInvoicePost drafts a receipt on posting; falls back
    /// to Supplier.DefaultWarehouseId when unset. Nullable because an invoice for services, or one
    /// raised in a company that never auto-creates receipts, has no warehouse to name.
    /// </summary>
    public long? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1;
    public SupplierPaymentTerms PaymentTerms { get; set; }

    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    /// <summary>إجمالي ما دُفع للمورد مقابل هذه الفاتورة حتى الآن — يتراكم من كل Voucher
    /// (VoucherType.Payment, CounterpartyType.Supplier, RelatedInvoiceId = هذه الفاتورة) بمجرد
    /// ترحيله (PostVoucherCommand)، ويرفع Status إلى PartiallyPaid أو Paid تبعًا لذلك.</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>نقل، شحن، جمارك — تُوزَّع على البنود حسب AdditionalCostAllocationMethod وتُحمَّل على
    /// تكلفة المخزون (rule 6) لو PurchaseCycleSettings.CapitalizeAdditionalCosts = true.</summary>
    public decimal AdditionalCosts { get; set; }
    public CostAllocationMethod? AdditionalCostAllocationMethod { get; set; }

    public decimal? CommissionRate { get; set; }
    public decimal? CommissionAmount { get; set; }
    public long? CommissionAccountId { get; set; }

    /// <summary>
    /// The entry the posting engine made when this invoice was posted. Null when the company had no
    /// active template for PURCHASING_PURCHASE_INVOICE at the time — that is how an invoice posted
    /// before accounting was switched on is told apart from one that has its entry.
    /// </summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}

public class PurchaseInvoiceLine : AuditableEntity
{
    public long PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>
    /// سطر أمر الشراء اللي السطر ده بيفوتره (Remarks6). بيتسجّل للسطور اللي اتحمّلت من الأمر،
    /// وبيفضل null للسطر اليدوي وللفاتورة اللي من غير أمر أصلًا. هو اللي بيخلّي الكمية متسقفة
    /// بالمتبقي وبيخلّي رجوع الكميات عند التعديل أو الإلغاء ممكن.
    /// </summary>
    public long? PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>الكمية المستلمة فعلاً المقابلة لهذا البند (من إذن الإضافة المرتبط، لو وُجد) —
    /// تُستخدم لمطابقة المفوتَر بالمستلَم فعليًا.</summary>
    public decimal ReceivedQuantity { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? DiscountAmount { get; set; }

    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    /// <summary>
    /// Base units in one UnitId (1 for the item's base unit), copied when the line is saved so a
    /// later change to the item's conversions does not rewrite this document (Fixes-Batch-2026-09-19).
    /// </summary>
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>The invoiced quantity in the item's base unit: Quantity × UnitFactor — what stock moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>UnitPrice per base unit: ÷ UnitFactor — what stock is valued at.</summary>
    public decimal BaseUnitCost { get; set; }

    /// <summary>حصة هذا البند من AdditionalCosts — محسوبة حسب طريقة التوزيع.</summary>
    public decimal AllocatedAdditionalCost { get; set; }

    /// <summary>للتوزيع اليدوي (Manual) فقط.</summary>
    public decimal? AllocationPercentage { get; set; }

    /// <summary>لتوزيع المصروفات الإضافية بطريقة ByWeight.</summary>
    public decimal? Weight { get; set; }
}
