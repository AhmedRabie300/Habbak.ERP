using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Sales;

/// <summary>حالة عرض السعر (04-Module-Sales.md, section 4.1). لا يوجد Job مجدول في هذا الكود
/// (00-Project-Overview.md) يُحوّل العروض تلقائيًا لـ`Expired` — يُتحقَّق من `ValidUntil` وقت
/// محاولة التحويل لأمر بيع فقط (قاعدة 14)، فتبقى هذه القيمة يدوية/نظرية حتى تُبنى مهمة مجدولة.</summary>
public enum SalesQuoteStatus
{
    Draft = 1,
    Sent = 2,
    Accepted = 3,
    Rejected = 4,
    Expired = 5,
    Converted = 6
}

/// <summary>عرض سعر (04-Module-Sales.md, section 2.3) — screen #5.</summary>
public class SalesQuote : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string QuoteNumber { get; set; } = null!;
    public DateOnly QuoteDate { get; set; }
    public DateOnly ValidUntil { get; set; }

    public SalesQuoteStatus Status { get; set; } = SalesQuoteStatus.Draft;

    public decimal Subtotal { get; set; }

    public ICollection<SalesQuoteLine> Lines { get; set; } = new List<SalesQuoteLine>();
}

public class SalesQuoteLine : AuditableEntity
{
    public long SalesQuoteId { get; set; }
    public SalesQuote? SalesQuote { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}
