using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.POS;

/// <summary>نقطة بيع (05-Module-POS-Shifts.md, section 2.1) — screen مبدئي. كل نقطة بيع تابعة
/// لفرع واحد دايمًا عبر عمود مباشر (قاعدة 28) — لا بديل عبر بُعد تحليلي.</summary>
public class POSTerminal : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    /// <summary>المخزن اللي بيتخصم منه المخزون وقت ترحيل POSInvoice (قاعدة 14) — مش في جدول
    /// الحقول الأصلي بالمواصفة، إضافة ضرورية لتفعيل التكامل الفعلي مع IStockMovementService
    /// (نفس دور DeliveryOrder.WarehouseId في 04-Module-Sales.md). Nullable عشان الأجهزة
    /// الموجودة بالفعل من المرحلة السابقة تفضل شغالة بدون تعديل فوري.</summary>
    public long? DefaultWarehouseId { get; set; }
    public Warehouse? DefaultWarehouse { get; set; }
}
