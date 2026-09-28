using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>قاعدة 15: تُنشأ Posted مباشرة دايمًا — لا مسار Draft. Voided/Returned مؤجَّلة لمرحلة
/// POSReturn اللاحقة (غير قابلة للوصول في هذا البناء بعد).</summary>
public enum POSInvoiceStatus
{
    Posted = 1,
    Voided = 2,
    Returned = 3
}

/// <summary>حالة إرسال الإيصال الإلكتروني (ETA) — تتبُّع الحالة فقط، بدون تكامل فعلي مع واجهة ETA
/// (محتاج شهادة توقيع وSandbox مش متاحين، قرار جلسة الاستشارة رقم 10). NotApplicable لما
/// BranchPOSSettings.ETAReceiptEnabled يكون معطّل وقت إنشاء الفاتورة.</summary>
public enum ETAReceiptStatus
{
    NotApplicable = 0,
    Pending = 1,
    Sent = 2,
    Failed = 3
}

/// <summary>
/// فاتورة نقطة بيع (05-Module-POS-Shifts.md, section 2.3) — قاعدة 14: بتتنشأ لحظة إتمام الدفع
/// الكامل على Check، وبترحّل خصم المخزون (TransactionType.POSSale) في نفس الـTransaction. لا يوجد
/// PublicId (نفس قرار كل كيانات الموديول). JournalEntryId فاضل null دايمًا — نفس تأجيل
/// IPostingService المُتَّبع بالفعل مع كل مستندات Sales/Purchasing (مفيش قيد محاسبي آلي لسه في كل
/// الكود). LoyaltyPointsEarned (قاعدة 29) مش متسجّل هنا كمان لنفس السبب: SalesInvoice نفسها لسه
/// ما بتكسبش نقاط فعليًا، فمفيش داعي POSInvoice تسبقها بسلوك غير متسق.
/// </summary>
public class POSInvoice : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public long CheckId { get; set; }
    public Check? Check { get; set; }

    public string InvoiceNumber { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }

    public long? CustomerId { get; set; }
    public CheckOrderType OrderType { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>خصم يدوي على مستوى الفاتورة (مراجعة 2026-09-13) — منفصل عن DiscountAmount (إجمالي
    /// خصومات البنود). القيمة الفعلية بالجنيه بعد التحويل (النسبة المئوية أو المبلغ الثابت المُطبَّق
    /// وقت الدفع)، مش النوع/القيمة الخام المُدخَلة (دول بيتمسحوا من Check نفسه بعد الترحيل).</summary>
    public decimal ManualDiscountAmount { get; set; }

    /// <summary>مراجعة 2026-09-13، قاعدة 12 — عدد النقاط المُستبدَلة فعليًا على هذه الفاتورة، وقيمتها
    /// بالجنيه (LoyaltyPointsRedeemed × LoyaltyProgramSettings.PointsRedemptionValue وقت الترحيل).
    /// LoyaltyTransaction المرتبط بيحمل نفس القيمة بالسالب على Customer.LoyaltyPointsBalance.</summary>
    public decimal LoyaltyPointsRedeemed { get; set; }
    public decimal LoyaltyDiscountAmount { get; set; }

    public decimal ServiceChargeAmount { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    public POSInvoiceStatus Status { get; set; } = POSInvoiceStatus.Posted;
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ETAReceiptStatus ETAReceiptStatus { get; set; } = ETAReceiptStatus.NotApplicable;

    public ICollection<POSInvoiceLine> Lines { get; set; } = new List<POSInvoiceLine>();
    public ICollection<POSPayment> Payments { get; set; } = new List<POSPayment>();
}

public class POSInvoiceLine : AuditableEntity
{
    public long POSInvoiceId { get; set; }
    public POSInvoice? POSInvoice { get; set; }

    public int LineNumber { get; set; }
    public long ItemId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// What this line actually cost to fulfil, frozen at the moment of sale (rule 42) — the posting
    /// engine's COGS figure reads this and never recalculates, so a later change to the warehouse's
    /// average must not move an invoice that has already been issued.
    ///
    /// For a real-time item this is the cost of the recipe components consumed, not the finished
    /// good's own average: a latte is assembled at the till and never stocked, so its own average
    /// is zero and would understate COGS to nothing.
    /// </summary>
    public decimal UnitCost { get; set; }
}
