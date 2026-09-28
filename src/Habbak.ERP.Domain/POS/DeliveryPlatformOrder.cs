using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>
/// طلب وارد من منصة توصيل خارجية (05-Module-POS-Shifts.md, section 2.3، قاعدة 17؛
/// 00-Project-Overview.md قسم 5.2) — (PlatformName, PlatformOrderId) فريدين لمنع معالجة نفس الطلب
/// مرتين، سواء من إعادة إرسال تلقائي من المنصة نفسها أو Retry. مفيش تكامل API فعلي مع Talabat/
/// Elmenus (يحتاج مفاتيح/عقود تجارية غير متاحة) — هذا endpoint بيمثّل نقطة الاستقبال اللي منصة
/// حقيقية هتنادي عليها webhook، وبيُستخدَم حاليًا كإدخال يدوي/محاكاة من طرف الكاشير.
/// </summary>
public class DeliveryPlatformOrder : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string PlatformName { get; set; } = null!;
    public string PlatformOrderId { get; set; } = null!;

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public long? CheckId { get; set; }
    public Check? Check { get; set; }
}
