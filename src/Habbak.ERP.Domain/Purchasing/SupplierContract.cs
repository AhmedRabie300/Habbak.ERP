using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>Section 6 has no explicit state machine for this entity — Active is the only reachable
/// value here: Expired would need a scheduled job (this codebase has none) to flip it once EndDate
/// passes, so it stays forward-declared and unreachable, same pattern as every other
/// job-dependent status in this module.</summary>
public enum SupplierContractStatus
{
    Active = 1,
    Expired = 2,
    Cancelled = 3,
    Archived = 4
}

/// <summary>
/// عقد مورد (03-Module-Purchasing.md, section 4.10) — screen #10. Rule 20 ("عقود الموردين تُستخدم
/// في تحديد الأسعار التلقائية... النظام يقترح سعر العقد") is NOT wired into CreatePurchaseOrderCommand
/// — that would mean touching the already-shipped, tested PurchaseOrder screen for a "nice to have"
/// price suggestion; deferred, this screen is self-contained CRUD only for now.
/// </summary>
public class SupplierContract : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string ContractNumber { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool AutoRenew { get; set; }

    public SupplierContractStatus Status { get; set; } = SupplierContractStatus.Active;

    public string? Notes { get; set; }

    public ICollection<ContractItem> Items { get; set; } = new List<ContractItem>();
}

public class ContractItem : AuditableEntity
{
    public long SupplierContractId { get; set; }
    public SupplierContract? SupplierContract { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal? DiscountPercentage { get; set; }
}
