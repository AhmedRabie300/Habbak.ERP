using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

public enum PurchaseOrderDeliveryTerms
{
    FOB = 1,
    CIF = 2,
    EXW = 3
}

/// <summary>
/// Section 6.3's state machine. This pass implements Draft → Sent → Confirmed plus the
/// Cancelled/Rejected terminal branches — PartiallyReceived/FullyReceived/Invoiced/Closed are
/// reachable only once GoodsReceipt/PurchaseInvoice exist (neither is built yet), the same
/// forward-declared-enum-value pattern already used for PurchaseRequestStatus.Converted/Archived.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 1,
    Sent = 2,
    Confirmed = 3,
    PartiallyReceived = 4,
    FullyReceived = 5,
    /// <summary>كل سطوره اتفوترت بالكامل.</summary>
    Invoiced = 6,

    Closed = 7,
    Cancelled = 8,
    Rejected = 9,
    Archived = 10,

    /// <summary>فيه سطور اتفوترت وسطور لسه (Remarks6) — بيتحسب تلقائيًا مع كل فاتورة.</summary>
    PartiallyInvoiced = 11
}

/// <summary>أمر شراء (03-Module-Purchasing.md, section 4.4) — screen #4، محور دورة المشتريات.</summary>
public class PurchaseOrder : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateOnly OrderDate { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>مرجع اختياري لطلب الشراء اللي اتحوّل لهذا الأمر (rule 2's auto-copy) —
    /// SubmitPurchaseRequestCommand's Approved request becomes Converted the moment this is set.</summary>
    public long? PurchaseRequestId { get; set; }
    public PurchaseRequest? PurchaseRequest { get; set; }

    /// <summary>
    /// طلب عروض الأسعار اللي الأمر ده اتعمل منه. اختياري عمومًا، وإلزامي لما
    /// PurchaseCycleSettings.RequiresQuotation تكون مفعّلة (Remarks4, item 6) — ساعتها لازم يكون
    /// RFQ في حالة Awarded ومورد الأمر هو المورد اللي رسا عليه.
    /// </summary>
    public long? RFQId { get; set; }
    public RequestForQuotation? RFQ { get; set; }

    /// <summary>العرض المختار من عروض الأسعار — لسه مش مُفعَّل لنفس السبب.</summary>
    public long? SelectedRFQSupplierQuoteId { get; set; }

    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1;
    public SupplierPaymentTerms PaymentTerms { get; set; }
    public PurchaseOrderDeliveryTerms? DeliveryTerms { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    public long? ApprovalInstanceId { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine : AuditableEntity
{
    public long PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>سطر طلب الشراء اللي السطر ده اتحمّل منه (Remarks7) — للتتبع وحساب
    /// PurchaseRequestLine.OrderedQuantity. Null لسطر يدوي مش من طلب شراء.</summary>
    public long? PurchaseRequestLineId { get; set; }
    public PurchaseRequestLine? PurchaseRequestLine { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>تُحدَّث مع كل إذن إضافة يرتبط بهذا الأمر.</summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>
    /// اللي اتفوتر من السطر ده (Remarks6) — بيزيد مع كل فاتورة بتتحفظ على الأمر وبيرجع لما الفاتورة
    /// تتعدّل أو تتلغي. بوحدة السطر نفسه (مش الوحدة الأساسية) لأن المقارنة بتتم مع Quantity.
    /// الفرق (Quantity − InvoicedQuantity) هو المتبقي اللي الفاتورة الجاية تقدر تاخده.
    /// </summary>
    public decimal InvoicedQuantity { get; set; }

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

    /// <summary>The ordered quantity in the item's base unit: Quantity × UnitFactor — what stock moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>UnitPrice per base unit: ÷ UnitFactor — what stock is valued at.</summary>
    public decimal BaseUnitCost { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    /// <summary>لتوزيع المصروفات الإضافية بطريقة ByWeight لاحقًا (section 4.5).</summary>
    public decimal? Weight { get; set; }
}
