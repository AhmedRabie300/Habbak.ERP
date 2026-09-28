using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

public enum PurchaseRequestPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

/// <summary>Section 6.1's state machine. Converted/Archived are reachable once PurchaseOrder
/// exists (Converted) or a housekeeping action is added later (Archived) — this pass implements
/// Draft → PendingApproval → Approved, plus the Rejected/Cancelled terminal branches; the enum
/// values for the later stages are declared now so the schema doesn't need another migration once
/// PurchaseOrder is built.</summary>
public enum PurchaseRequestStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Converted = 4,
    Rejected = 5,
    Cancelled = 6,
    Archived = 7,

    /// <summary>فيه سطور اتحوّلت لأمر شراء وسطور لسه (Remarks7) — بيتحسب تلقائيًا مع كل أمر شراء،
    /// نظير PurchaseOrderStatus.PartiallyInvoiced.</summary>
    PartiallyConverted = 8
}

/// <summary>طلب شراء (03-Module-Purchasing.md, section 4.2) — screen #2.</summary>
public class PurchaseRequest : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string RequestNumber { get; set; } = null!;
    public DateOnly RequestDate { get; set; }
    public long RequestedByUserId { get; set; }
    public PurchaseRequestPriority Priority { get; set; } = PurchaseRequestPriority.Normal;
    public string? Reason { get; set; }
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public long? ApprovalInstanceId { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
}

public class PurchaseRequestLine : AuditableEntity
{
    public long PurchaseRequestId { get; set; }
    public PurchaseRequest? PurchaseRequest { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>الوحدة المطلوب الشراء بيها — نفس كيان `UnitOfMeasure` المستخدم في موديول المخازن،
    /// مش نوع جديد خاص بالمشتريات.</summary>
    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    /// <summary>
    /// Base units in one UnitId (1 for the item's base unit), copied when the line is saved so a
    /// later change to the item's conversions does not rewrite this document (Fixes-Batch-2026-09-19).
    /// </summary>
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>The requested quantity in the item's base unit: Quantity × UnitFactor — what stock moves by.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>
    /// اللي اتحوّل لأمر شراء من السطر ده (Remarks7) — بيزيد مع كل أمر شراء بيرتبط بالطلب وبيرجع لما
    /// الأمر يتعدّل أو يتلغي/يترفض. بوحدة السطر نفسه (مش الوحدة الأساسية) لأن المقارنة بتتم مع
    /// Quantity. الفرق (Quantity − OrderedQuantity) هو المتبقي اللي الأمر الجاية تقدر تاخده.
    /// </summary>
    public decimal OrderedQuantity { get; set; }

    public string? Notes { get; set; }
}
