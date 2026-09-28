using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>Section 6.2's diagram shows an extra "مغلق" (Closed) node after Awarded, but the field
/// table (section 4.3) never lists it as an enum value — only these six. Treated as a doc
/// inconsistency (same class as AllowInvoiceWithoutReceipt): Awarded is the terminal success state
/// here, Closed is never reached.</summary>
public enum RFQStatus
{
    Draft = 1,
    Sent = 2,
    UnderReview = 3,
    Awarded = 4,
    Cancelled = 5,
    Archived = 6
}

public enum RFQSupplierStatus
{
    Pending = 1,
    Responded = 2,
    Declined = 3
}

/// <summary>
/// طلب عروض أسعار (03-Module-Purchasing.md, section 4.3) — screen #3, "مع مقارنة عروض الموردين".
/// No PurchaseOrder.RFQId/SelectedRFQSupplierQuoteId wiring here — CreatePurchaseOrderCommand
/// already ships and is tested without it; a user awards an RFQ and then creates the PurchaseOrder
/// by hand using the winning quote's numbers, same scope reduction as SupplierContract's price
/// suggestion.
/// </summary>
public class RequestForQuotation : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string RFQNumber { get; set; } = null!;
    public DateOnly RFQDate { get; set; }

    public long? PurchaseRequestId { get; set; }
    public PurchaseRequest? PurchaseRequest { get; set; }

    public RFQStatus Status { get; set; } = RFQStatus.Draft;

    public DateOnly? RequiredDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<RFQLine> Lines { get; set; } = new List<RFQLine>();
    public ICollection<RFQSupplier> Suppliers { get; set; } = new List<RFQSupplier>();
}

public class RFQLine : AuditableEntity
{
    public long RFQId { get; set; }
    public RequestForQuotation? RFQ { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }

    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public ICollection<RFQSupplierQuote> Quotes { get; set; } = new List<RFQSupplierQuote>();
}

public class RFQSupplier : AuditableEntity
{
    public long RFQId { get; set; }
    public RequestForQuotation? RFQ { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public RFQSupplierStatus Status { get; set; } = RFQSupplierStatus.Pending;
    public DateOnly? ResponseDate { get; set; }

    public ICollection<RFQSupplierQuote> Quotes { get; set; } = new List<RFQSupplierQuote>();
}

/// <summary>عرض سعر من مورد لبند بعينه. IsExpired is deliberately NOT a stored column — the doc's
/// own field table marks it "محسوب من ValidUntil" (computed from ValidUntil), so it's derived at
/// read/selection time (DateOnly.Today &gt; ValidUntil) instead of needing a scheduled job to keep a
/// stored flag in sync, which this codebase has no infrastructure for.</summary>
public class RFQSupplierQuote : AuditableEntity
{
    public long RFQSupplierId { get; set; }
    public RFQSupplier? RFQSupplier { get; set; }

    public long RFQLineId { get; set; }
    public RFQLine? RFQLine { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public int? DeliveryDays { get; set; }
    public DateOnly ValidUntil { get; set; }

    public bool IsSelected { get; set; }
    public string? Notes { get; set; }
}
