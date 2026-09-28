using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Sales;

/// <summary>حالة أمر البيع (04-Module-Sales.md, section 4.2). PartiallyDelivered/Delivered/Invoiced
/// غير قابلة للوصول في هذه المرحلة (تحتاج DeliveryOrder/SalesInvoice غير مبنيَين بعد) — نفس نمط
/// PurchaseOrderStatus.Invoiced/Closed المُعلَنة سلفًا قبل بناء الكود اللي يصل لها فعليًا.</summary>
public enum SalesOrderStatus
{
    Draft = 1,
    Confirmed = 2,
    PartiallyDelivered = 3,
    Delivered = 4,
    Invoiced = 5,
    Rejected = 6,
    Cancelled = 7
}

/// <summary>أمر بيع (04-Module-Sales.md, section 2.3) — screen #6.</summary>
public class SalesOrder : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateOnly OrderDate { get; set; }

    /// <summary>لو ناتج تحويل عرض سعر (قاعدة 15).</summary>
    public long? SourceQuoteId { get; set; }
    public SalesQuote? SourceQuote { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    public decimal Subtotal { get; set; }

    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}

public class SalesOrderLine : AuditableEntity
{
    public long SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>تُحدَّث لاحقًا عبر ترحيل DeliveryOrder — تبقى صفر حتى بناء موديول التسليم.</summary>
    public decimal DeliveredQuantity { get; set; }
}
