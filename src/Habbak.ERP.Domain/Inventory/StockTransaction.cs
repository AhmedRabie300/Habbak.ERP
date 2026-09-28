using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// سجل الحركة التاريخي (02-Module-Inventory-Manufacturing.md, section 2.2) — سجل غير قابل
/// للتعديل بعد إنشائه (نفس فلسفة JournalEntry المرحّل)؛ أي تصحيح يتم بحركة عكسية جديدة، مش
/// بتعديل السجل القديم.
/// </summary>
public class StockTransaction : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public TransactionType TransactionType { get; set; }

    /// <summary>قيمة موجبة دايمًا — الاتجاه محدد من TransactionType (قاعدة القسم 2.2).</summary>
    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }
    public DateOnly TransactionDate { get; set; }

    /// <summary>إلزامي لو Item.IsTracked=true وحركة داخلة — يُحسَب افتراضيًا كـ
    /// TransactionDate + Item.ShelfLifeDays لكن قابل للتعديل يدويًا (قاعدة 32).</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>مرجع مرن (Polymorphic) — قيم ثابتة: WarehouseDocument, InventoryCount,
    /// ProductionOrder, POSSale.</summary>
    public string? SourceDocumentType { get; set; }
    public long? SourceDocumentId { get; set; }

    /// <summary>إلزامي لو Item.IsTracked=true (قاعدة 8) — رقم تعريفي للدفعة فقط، بدون تاريخ مشفّر.</summary>
    public string? BatchNumber { get; set; }

    public long? JournalEntryId { get; set; }
}
