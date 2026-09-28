using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Sales;

public enum SalesInvoicePaymentType
{
    Cash = 1,
    Credit = 2
}

/// <summary>حالة فاتورة المبيعات (04-Module-Sales.md, section 4.3). `PendingApproval` مُعلَنة
/// لاكتمال المخطط فقط — لا يوجد علم مكافئ لـ`RequiresApprovalForInvoice` في `SalesCycleSettings`
/// (القسم 2.6 لا يعرّف سوى 5 أعلام، ولا واحد منها يتحكم في اعتماد الفاتورة)، فيبقى الانتقال إليها
/// غير قابل للوصول حاليًا — نفس نمط `PurchaseOrderStatus.Invoiced/Closed` في المشتريات.</summary>
public enum SalesInvoiceStatus
{
    Draft = 1,
    PendingApproval = 2,
    Posted = 3,
    Rejected = 4,
    Cancelled = 5
}

/// <summary>فاتورة مبيعات (04-Module-Sales.md, section 2.4) — screen #7. لا يوجد استدعاء
/// `IPostingService` هنا — `JournalEntryId` يبقى فارغًا، بنفس نمط `PurchaseInvoice` في المشتريات
/// لحين بناء محرك ترحيل حقيقي.</summary>
public class SalesInvoice : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string InvoiceNumber { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }

    /// <summary>لو ناتج تحويل أمر بيع.</summary>
    public long? SourceOrderId { get; set; }
    public SalesOrder? SourceOrder { get; set; }

    public SalesInvoicePaymentType PaymentType { get; set; }

    /// <summary>إلزامي `true` لو الفاتورة تجاوزت حد الائتمان وتم قبول التجاوز صراحة (قاعدة 1).</summary>
    public bool CreditLimitOverrideApproved { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }

    /// <summary>The entry the posting engine made for this document; null when its screen had no active template.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
}

public class SalesInvoiceLine : AuditableEntity
{
    public long SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>
    /// What the goods on this line actually cost, frozen for the posting engine's COGS figure
    /// (rule 42).
    ///
    /// Stamped when the matching delivery order posts, not when the invoice line is written: a
    /// sales invoice carries no warehouse of its own, so until the goods are actually despatched
    /// there is no average cost to snapshot. Null means nothing has been delivered against this
    /// line yet — and therefore that there is no cost of sale to post either.
    /// </summary>
    public decimal? UnitCost { get; set; }
}
