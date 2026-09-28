using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

public enum CheckOrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3
}

/// <summary>مراجعة 2026-09-13، بند 3.2 — خصم يدوي على مستوى الفاتورة كاملة، منفصل عن
/// CheckLine.DiscountAmount لكل بند. Percentage تُحسب على (Subtotal − إجمالي خصومات البنود)،
/// Fixed تُطبَّق كقيمة ثابتة مباشرة (تُقصّ عند صافي البنود لو تجاوزتها).</summary>
public enum POSManualDiscountType
{
    Percentage = 1,
    Fixed = 2
}

/// <summary>Completed تُبنى في مرحلة الدفع (POSPayment/POSInvoice، لسه غير مبني) — مش قابلة للوصول
/// بعد في هذا البناء.</summary>
public enum CheckStatus
{
    Open = 1,
    Held = 2,
    Completed = 3,
    Rejected = 4,
    Cancelled = 5,
    Merged = 6
}

/// <summary>
/// شيك (05-Module-POS-Shifts.md, section 2.2) — الكيان المحوري لنظام الشيكات المتعددة. لا يوجد
/// PublicId (نفس القرار المُتَّبع فعليًا في كل كيانات الموديول لحد الآن — يخالف جدول الحقول في
/// المواصفة عمدًا). لا يوجد Subtotal/Total مُخزَّن هنا — دول محسوبون فقط وقت العرض؛ يُخزَّنوا فعليًا
/// على POSInvoice وقت الترحيل (مرحلة لاحقة).
/// </summary>
public class Check : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public string CheckCode { get; set; } = null!;

    public long? TableId { get; set; }
    public Table? Table { get; set; }

    public CheckOrderType OrderType { get; set; }
    public CheckStatus Status { get; set; } = CheckStatus.Open;

    public long? CustomerId { get; set; }

    public long? MergedIntoCheckId { get; set; }
    public Check? MergedIntoCheck { get; set; }

    /// <summary>خصم يدوي على الفاتورة (مراجعة 2026-09-13) — الثلاثة null معًا يعني مفيش خصم مطبَّق
    /// حاليًا. يُمسَحوا تلقائيًا عند إتمام الدفع (القيمة الفعلية تُحفَظ على POSInvoice.ManualDiscountAmount
    /// بدل كده).</summary>
    public POSManualDiscountType? ManualDiscountType { get; set; }
    public decimal? ManualDiscountValue { get; set; }
    public string? ManualDiscountReason { get; set; }

    /// <summary>مراجعة 2026-09-13 — عدد النقاط اللي العميل طلب استبدالها على هذه الفاتورة (معاينة
    /// فقط لحد إتمام الدفع؛ الخصم الفعلي من Customer.LoyaltyPointsBalance ما بيحصلش إلا في
    /// CompleteCheckPaymentCommand، قاعدة 12). null/صفر يعني مفيش استبدال مطلوب حاليًا. يتطلب
    /// CustomerId مُحدَّد و BranchPOSSettings.LoyaltyRedemptionEnabledAtPOS مفعَّل.</summary>
    public decimal? LoyaltyPointsToRedeem { get; set; }

    public ICollection<CheckLine> Lines { get; set; } = new List<CheckLine>();
}

/// <summary>بند الشيك — SentToKitchenAt يفصل بين حذف قبل الإرسال للمطبخ (بدون احتكاك) وحذف بعده
/// (سبب إلزامي + توثيق دائم عبر CheckLineVoid، قرار جلسة الاستشارة قبل التنفيذ رقم 8).</summary>
public class CheckLine : AuditableEntity
{
    public long CheckId { get; set; }
    public Check? Check { get; set; }

    public int LineNumber { get; set; }

    public long ItemId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsPriceManuallyOverridden { get; set; }
    public string? Note { get; set; }

    public DateTime? SentToKitchenAt { get; set; }
}

/// <summary>سجل توثيق دائم لحذف بند بعد إرساله للمطبخ — الصف بيتسجل وقت الحذف نفسه، مش تعديل على
/// CheckLine (اللي بيتشال فعليًا من الشيك). لا يوجد نظام Audit Trail عام في الكود لحد الآن (نفس
/// تأجيل نظام الصلاحيات)، فده أقرب حل عملي محدد النطاق بدل بناء إطار عام غير مطلوب الآن.</summary>
public class CheckLineVoid : AuditableEntity
{
    public long CheckId { get; set; }
    public Check? Check { get; set; }

    public long ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Reason { get; set; } = null!;
    public long VoidedByUserId { get; set; }
    public DateTime VoidedAtUtc { get; set; }
}
