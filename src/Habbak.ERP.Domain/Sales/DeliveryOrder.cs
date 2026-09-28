using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Sales;

/// <summary>حالة أمر التسليم (04-Module-Sales.md, section 4.4) — مسودة → مرحّل (يؤثر على
/// StockBalance مباشرة)، أو مرفوض (نهائي). لا يوجد Cancelled في مخطط الحالة الأصلي لهذا المستند.</summary>
public enum DeliveryOrderStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3
}

/// <summary>
/// أمر تسليم (04-Module-Sales.md, section 2.4) — screen #8. يخدم كل سيناريوهات "صرف/تسليم
/// البضاعة" في دورة المبيعات (قاعدة 33) — لا يوجد كيان WarehouseDocument منفصل من نوع صرف مبيعات.
///
/// بالضبط واحد من SourceOrderId/SourceInvoiceId لازم يكون محدد، مش الاثنين معًا ولا لا واحد منهما
/// (قاعدة 31) — الأول يعني الدورة بدأت من أمر بيع، والثاني يعني الدورة بدأت من فاتورة مباشرة
/// (نمط InvoiceWithIssue) تتطلب إذن صرف تالٍ.
/// </summary>
public class DeliveryOrder : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>مخزن الصرف — يتأثر StockBalance مباشرة وقت الترحيل (قاعدة 17).</summary>
    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string DeliveryNumber { get; set; } = null!;
    public DateOnly DeliveryDate { get; set; }

    public long? SourceOrderId { get; set; }
    public SalesOrder? SourceOrder { get; set; }

    public long? SourceInvoiceId { get; set; }
    public SalesInvoice? SourceInvoice { get; set; }

    public DeliveryOrderStatus Status { get; set; } = DeliveryOrderStatus.Draft;

    /// <summary>The cost-of-sales entry made when the goods left; null when no active template.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<DeliveryOrderLine> Lines { get; set; } = new List<DeliveryOrderLine>();
}

public class DeliveryOrderLine : AuditableEntity
{
    public long DeliveryOrderId { get; set; }
    public DeliveryOrder? DeliveryOrder { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>إلزامي لو الصنف Item.IsTracked = true (قاعدة 8 — نفس قاعدة GoodsReceiptLine على
    /// الجانب الآخر من الحركة)، رغم أن المواصفة لا تذكرها صراحة في جدول حقول DeliveryOrderLine —
    /// بدونها يستحيل ترحيل أي صنف مُتتبَّع عبر أمر التسليم (StockMovementService يرفضه حتمًا).</summary>
    public string? BatchNumber { get; set; }
}
