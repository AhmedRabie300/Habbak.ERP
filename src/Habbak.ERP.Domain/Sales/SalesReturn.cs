using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Sales;

/// <summary>حالة مرتجع المبيعات (04-Module-Sales.md, section 2.4). على عكس PurchaseReturnStatus
/// (Draft/Posted/Cancelled/Archived, بدون Rejected)، مواصفة المبيعات تُعرِّف Rejected صراحة كحالة
/// منفصلة عن Cancelled — كلاهما نهائي من Draft فقط.</summary>
public enum SalesReturnStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// مرتجع مبيعات (04-Module-Sales.md, section 2.4) — screen #9. لا يوجد استدعاء IPostingService —
/// JournalEntryId يبقى فارغًا (قاعدة 19's "قيد عكسي" مؤجَّل)، بنفس القرار المُتَّخذ فعليًا في
/// PurchaseReturn على الجانب الآخر من الدورة (IPostingService غير مُفعَّل لأي مستند في المشتريات
/// أو المبيعات حتى الآن).
/// </summary>
public class SalesReturn : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>مخزن الاستلام — يزيد StockBalance وقت الترحيل (قاعدة 19).</summary>
    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public long? SourceInvoiceId { get; set; }
    public SalesInvoice? SourceInvoice { get; set; }

    public string ReturnNumber { get; set; } = null!;
    public DateOnly ReturnDate { get; set; }
    public string Reason { get; set; } = null!;

    public SalesReturnStatus Status { get; set; } = SalesReturnStatus.Draft;

    /// <summary>The entry the posting engine made for this document; null when its screen had no active template.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<SalesReturnLine> Lines { get; set; } = new List<SalesReturnLine>();
}

public class SalesReturnLine : AuditableEntity
{
    public long SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>إلزامي لو Item.IsTracked=true (قاعدة 8 المشتركة مع كل حركة مخزون) — نفس الاستثناء
    /// اللي احتجناه على DeliveryOrderLine.</summary>
    public string? BatchNumber { get; set; }
}
