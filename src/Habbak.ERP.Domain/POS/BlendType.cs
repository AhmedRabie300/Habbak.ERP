using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Domain.POS;

/// <summary>مراجعة 2026-09-13، بند 2.1 (شاشة استشاري التصنيع) — قائمة مخصّصة ومبسّطة لأنواع البن
/// المتاحة للخلط (اسم + سعر الجرام)، منفصلة عمدًا عن شاشة الأصناف العامة (Items) بقرار المستخدم
/// نفسه: مفيش موديول تحميص/تصنيع حقيقي مبني بعد يربط كل نوع بدفعة تحميص فعلية من المخزون، فالقائمة
/// دي مرجع بيانات بسيط لحد ما الموديول ده يتبني.
///
/// كل BlendType بيحمل ItemId لصنف خلفي (RawMaterial, IsSellable=false, IsStocked=false) يتولّد
/// تلقائيًا وقت الإنشاء — مش عشان "استخدام Items"، لكن لأن كل من QRTicketLine.ItemId (تذكرة الخلطة
/// نفسها) و CheckLine.ItemId (وقت مسح التذكرة وإضافتها لفاتورة) مفتاح أجنبي إلزامي على Item حقيقي في
/// الكود الحالي — الصنف الخلفي هو التفصيلة التقنية اللي بتخلي إعادة استخدام GenerateQRTicketCommand/
/// RedeemQRTicketCommand الموجودين بالفعل (المرحلة 1و من [[pos_module_status]]) ممكنة بدون أي تعديل
/// عليهم.</summary>
public class BlendType : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    public decimal PricePerGram { get; set; }

    public long ItemId { get; set; }
    public Item? Item { get; set; }

    public bool IsActive { get; set; } = true;
}
