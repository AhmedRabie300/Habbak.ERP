using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>Void/Refund على دفعة Completed محتاج صلاحية VoidRefundPayment (قاعدة 13) — غير قابل
/// للوصول في هذا البناء لعدم وجود نظام صلاحيات حقيقي بعد (نفس تأجيل باقي الموديول)، فكل الدفعات
/// في هذه المرحلة تفضل Completed دايمًا.</summary>
public enum POSPaymentStatus
{
    Completed = 1,
    Voided = 2,
    Refunded = 3
}

/// <summary>
/// دفعة (05-Module-POS-Shifts.md, section 2.3). AmountTendered/ChangeGiven/CashRoundingAdjustment
/// إضافات على الحقول الأصلية بالمواصفة — قرار جلسة الاستشارة قبل التنفيذ رقم 11 (تسجيل المبلغ
/// المدفوع نقدًا وحساب الباقي تلقائيًا) وBranchPOSSettings.CashRoundingIncrement. مفيش عمود
/// "IsCash" على PaymentMethod نفسها: أي دفعة ممكن الكاشير يسجّل عليها AmountTendered لو حبّ، مش
/// مربوطة بتصنيف طريقة الدفع — الإدخال نفسه هو الإشارة.</summary>
public class POSPayment : AuditableEntity
{
    public long POSInvoiceId { get; set; }
    public POSInvoice? POSInvoice { get; set; }

    public long PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal Amount { get; set; }

    public string? CardTransactionReference { get; set; }
    public decimal? MachineCommission { get; set; }

    /// <summary>المبلغ اللي فعليًا استلمه الكاشير نقدًا — null لو مفيش تقريب/دفع نقدي مسجَّل لهذه
    /// الدفعة. مش بديل عن Amount (اللي بيدخل في معادلة رقم 12)، بس أساس حساب Change/Rounding.</summary>
    public decimal? AmountTendered { get; set; }
    public decimal? ChangeGiven { get; set; }

    /// <summary>الفرق بين المبلغ المُقرَّب فعليًا للفئة النقدية الأقرب وAmount الحقيقي — تسجيلي
    /// بحت لأغراض مطابقة الكاش وقت إغلاق الوردية (قاعدة 6)، لا يدخل في معادلة رقم 12.</summary>
    public decimal CashRoundingAdjustment { get; set; }

    public POSPaymentStatus Status { get; set; } = POSPaymentStatus.Completed;
}
