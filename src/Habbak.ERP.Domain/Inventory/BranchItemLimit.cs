using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// حد الصنف للفرع (02-Module-Inventory-Manufacturing.md, section 2.4) — 2 من "الحدود الخمسة"
/// (قاعدة 29)، على مستوى حجم طلب التوريد الواحد، بمعزل تام عن حدود المخزون الفعلي في
/// ItemWarehouseSettings. يُدار كقسم فرعي داخل شاشة تعديل الصنف (نفس نمط ItemWarehouseSettings)
/// — الملف لا يخصّص له شاشة List/Edit منفصلة في قسم 5.
/// </summary>
public class BranchItemLimit : AuditableEntity
{
    public long BranchId { get; set; }

    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;

    /// <summary>أقل كمية مسموح بطلبها في الطلب الواحد — لو محدد ومفيش طلب أعلى منه، يُرفض الطلب.</summary>
    public decimal? MinRequestQuantity { get; set; }

    /// <summary>أقصى كمية مسموح بطلبها في الطلب الواحد.</summary>
    public decimal MaxRequestQuantity { get; set; }
}
