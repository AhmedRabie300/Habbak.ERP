using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>Drop و Pickup الاتنين خروج نقدية فعلي من الدرج (قاعدة 6) — Drop إيداع دوري للخزينة
/// الرئيسية لتقليل الكاش في الدرج لأسباب أمنية، Pickup تحصيل استثنائي بمعرفة مشرف/شركة نقل أموال؛
/// الاتنين بيقللوا ExpectedClosingCashAmount بنفس الطريقة، مش بيزودوه.</summary>
public enum DrawerMovementType
{
    Drop = 1,
    Pickup = 2
}

/// <summary>قاعدة 32: النوع Recorded لسه معلّق ومالوش أي تأثير على حساب الفرق المتوقع وقت إغلاق
/// الوردية — لازم يتحول Approved الأول. Rejected مُتاحة لاكتمال دورة الاعتماد (مش منصوص عليها
/// صراحة في قاعدة 32 لكن متسقة مع نمط الموافقة/الرفض المُستخدم في باقي الموديولات).</summary>
public enum DrawerMovementStatus
{
    Recorded = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// حركة درج (05-Module-POS-Shifts.md, section 3، قاعدة 6/32) — إيداع أو سحب من درج الكاشير أثناء
/// الوردية، بسير اعتماد إداري بسيط (Recorded → Approved/Rejected). لا يوجد نظام صلاحيات حقيقي بعد
/// (نفس تأجيل باقي الموديول) فأي مستخدم يقدر يعتمد — ApprovedByUserId مجرد long بدون FK، نفس نمط
/// CashierUserId في Shift.
/// </summary>
public class DrawerMovement : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DrawerMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }

    public DrawerMovementStatus Status { get; set; } = DrawerMovementStatus.Recorded;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}
