using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>Fixed source-document values (module doc, section 2.6) — kept as a plain string rather
/// than an enum because WasteRecord.SourceDocumentType is a free-form polymorphic tag shared with
/// StockMovementRequest.SourceDocumentType elsewhere in this module, not a closed domain concept.</summary>
public static class WasteSourceDocumentType
{
    public const string ProductionOrder = "ProductionOrder";
    public const string POSRealtimeLoss = "POSRealtimeLoss";
    public const string Manual = "Manual";
}

/// <summary>سجل هالك (02-Module-Inventory-Manufacturing.md, section 2.6) — screen #18 (متابعة الهالك).
/// ApprovalInstanceId is required once the waste's financial value (Quantity × Item.StandardCost)
/// crosses InventorySettings' threshold (rule 19); that settings screen isn't built yet (section 2.7),
/// so this pass leaves the field populated with null and defers the actual approval-chain check —
/// same deferral already applied to every other ApprovalInstanceId in this module pending a real
/// approvals engine.</summary>
public class WasteRecord : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal Quantity { get; set; }
    public DateOnly WasteDate { get; set; }
    public string Reason { get; set; } = null!;

    public string? SourceDocumentType { get; set; }
    public long? SourceDocumentId { get; set; }

    public long? ApprovalInstanceId { get; set; }

    /// <summary>The waste entry; null when no active template.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
