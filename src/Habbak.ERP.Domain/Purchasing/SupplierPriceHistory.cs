using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.Purchasing;

/// <summary>
/// سجل أسعار الموردين (03-Module-Purchasing.md, section 4.9) — screen #13 (تقرير، قراءة فقط).
/// صف واحد يُنشأ تلقائيًا لكل بند فاتورة شراء عند ترحيلها (PostPurchaseInvoiceCommand) — لا يوجد
/// إنشاء/تعديل يدوي، السجل بيتراكم بمرور الوقت ليتتبع تغيّر سعر الصنف عند كل مورد.
/// </summary>
public class SupplierPriceHistory : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public decimal UnitPrice { get; set; }

    public long UnitId { get; set; }
    public UnitOfMeasure? Unit { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public long? PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }

    public string? Notes { get; set; }
}
