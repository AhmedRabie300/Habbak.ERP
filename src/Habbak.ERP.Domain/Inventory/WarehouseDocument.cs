using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

public enum WarehouseDocumentType
{
    StockIn = 1,
    StockOut = 2,
    TransferOrder = 3,
    TransferReceipt = 4,
    InventoryAdjustment = 5,
    ProductionIssue = 6,
    ProductionReceipt = 7,

    /// <summary>Screen #7 (My Remarks/Remarks2.md, bugs 1.2/2.5/2.6 and feature 3.7) — redesigned
    /// from a single-line StockTransaction-only screen into a proper Master/Detail document,
    /// reusing this same WarehouseDocument/WarehouseDocumentLine pair rather than a parallel
    /// entity. Destination-only, exactly like StockIn.</summary>
    OpeningBalance = 8
}

public enum WarehouseDocumentStatus
{
    Draft = 1,
    Posted = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// مستند مخزني موحّد (02-Module-Inventory-Manufacturing.md, section 2.3) — نمط موحّد لكل أنواع
/// مستندات الحركة، نفس فلسفة Voucher في موديول الحسابات. هذه الدفعة تُفعِّل StockIn/StockOut/
/// TransferOrder/TransferReceipt — InventoryAdjustment/ProductionIssue/ProductionReceipt (تُنشأ
/// آليًا من شاشات لسه مش مبنية: دورة الجرد وأوامر الإنتاج) مؤجلة، رغم إن القيم موجودة في الـ enum
/// من الأول.
///
/// لا يتكامل مع IPostingService حاليًا: لا يوجد إعداد لحساب المخزون/التسوية في دليل الحسابات
/// بعد (InventorySettings الحالي فيه SlowMovingThresholdDays بس) — الترحيل هنا بيحدّث
/// StockBalance/StockTransaction عبر IStockMovementService فقط، بدون قيد محاسبي مرافق.
/// </summary>
public class WarehouseDocument : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public WarehouseDocumentType DocumentType { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public DateOnly DocumentDate { get; set; }

    /// <summary>إلزامي لـ StockOut وTransferOrder (وProductionIssue لاحقًا).</summary>
    public long? SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }

    /// <summary>إلزامي لـ StockIn وTransferReceipt (وProductionReceipt لاحقًا). ملحوظة: TransferOrder
    /// نفسه لا يحمل وجهة — الوجهة الفعلية بتتأكد وقت الاستلام (TransferReceipt)، والمسؤول عن
    /// البضاعة أثناء الفترة دي هو CustodyOfficerId (قاعدة 30).</summary>
    public long? DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    /// <summary>مرجع ذاتي — إلزامي لـ TransferReceipt فقط، بيشاور على TransferOrder.Id (قاعدة 37).</summary>
    public long? RelatedWarehouseDocumentId { get; set; }
    public WarehouseDocument? RelatedWarehouseDocument { get; set; }

    /// <summary>إلزامي لـ TransferOrder فقط، ومنسوخ تلقائيًا لـ TransferReceipt المرتبط وقت
    /// إنشاء الاستلام (قاعدة 34).</summary>
    public long? CustodyOfficerId { get; set; }
    public CustodyOfficer? CustodyOfficer { get; set; }

    public WarehouseDocumentStatus Status { get; set; } = WarehouseDocumentStatus.Draft;

    public long? ApprovalInstanceId { get; set; }
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public string? Notes { get; set; }

    public ICollection<WarehouseDocumentLine> Lines { get; set; } = new List<WarehouseDocumentLine>();
}
