using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>وضع تشغيل نقطة البيع (00-Project-Overview.md, section 14.4.5). المزامنة الفعلية
/// (Local Agent/Offline Sync Service) مرحلة لاحقة منفصلة تمامًا عن هذا البناء — هذا الحقل يُخزَّن
/// الآن فقط للاكتمال، لا يوجد أي كود يقرأه بعد.</summary>
public enum POSOperationMode
{
    CloudOnly = 1,
    OfflineOnly = 2,
    HybridAutoSync = 3
}

/// <summary>
/// إعدادات نقطة البيع لكل فرع (05-Module-POS-Shifts.md + قرارات جلسة الاستشارة قبل التنفيذ) — صف
/// واحد لكل فرع، بنفس نمط SalesCycleSettings/PurchaseCycleSettings. كل الأعلام دي إضافات على
/// المواصفة الأصلية اتفق عليها العميل صراحة كخيارات قابلة للتفعيل لكل فرع على حدة، مش سلوك عام
/// واحد للنظام كله.
/// </summary>
public class BranchPOSSettings : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public POSOperationMode OperationMode { get; set; } = POSOperationMode.HybridAutoSync;

    public bool TipsEnabled { get; set; }

    public bool ServiceChargeEnabled { get; set; }
    public decimal ServiceChargeRate { get; set; }

    /// <summary>ضريبة القيمة المضافة على فاتورة نقطة البيع — تُحسب تلقائيًا على (الإجمالي بعد
    /// الخصم) فقط، قبل احتساب الخدمة والبقشيش (مطابقةً لأبسط قراءة ممكنة لقاعدة الضريبة، ومطابقة
    /// لنموذج الموك اب المرجعي Coffee_ERP_Full_System_Mockup.html). القيمة الافتراضية 14% (السعر
    /// الموحّد في مصر) لكنها قابلة للتعديل لكل فرع، ومعطّلة افتراضيًا مطابقةً لنفس نمط
    /// ServiceChargeEnabled أعلاه — علم اختياري لكل فرع، مش سلوك عام مفروض.</summary>
    public bool VatEnabled { get; set; }
    public decimal VatRate { get; set; } = 14m;

    /// <summary>يسمح بأكتر من POSPayment بنفس PaymentMethodId على نفس الفاتورة (تقسيم الدفع بين
    /// أكتر من شخص) — الدفع المختلط بطرق مختلفة مسموح دايمًا بغض النظر عن هذا العلم.</summary>
    public bool AllowSplitPayment { get; set; }

    public bool LoyaltyRedemptionEnabledAtPOS { get; set; }

    /// <summary>الإيصال الإلكتروني (E-Receipt) — بروتوكول ETA مختلف عن الفاتورة الإلكترونية
    /// (E-Invoice) المستخدم في 04-Module-Sales.md. لا يوجد ربط فعلي بواجهة ETA بعد (يحتاج شهادة
    /// توقيع وSandbox) — هذا العلم يتحكم فقط في وضع POSInvoice.ETAReceiptStatus كـPending من
    /// عدمه، مطابقةً لنفس تأجيل IPostingService المُتَّبَع في كل موديول لحد الآن.</summary>
    public bool ETAReceiptEnabled { get; set; }

    /// <summary>صفر = تقريب معطّل. غير كده، أقرب فئة يُقرَّب لها إجمالي الدفعة النقدية فقط (مش
    /// الفاتورة نفسها) — الفرق يُسجَّل في POSPayment.CashRoundingAdjustment حتى تفضل معادلة "مجموع
    /// الدفعات = الإجمالي بالضبط" (قاعدة 12) متزنة محاسبيًا.</summary>
    public decimal CashRoundingIncrement { get; set; }

    /// <summary>فرق إغلاق الوردية اللي يتجاوزه يتطلب اعتماد (قاعدة 5) — مقترح المواصفة 100 ج.م
    /// كقيمة افتراضية معقولة، قابلة للتخصيص لكل فرع.</summary>
    public decimal MaxAllowedShiftCashDifference { get; set; } = 100m;

    /// <summary>
    /// When this branch's POS sales reach the ledger (decided 2026-09-18): one entry per shift by
    /// default, switchable at any time. Switching is safe because every trigger posts only invoices
    /// that have no entry yet (POSInvoice.JournalEntryId), so nothing is posted twice or skipped.
    /// </summary>
    public POSPostingMode PostingMode { get; set; } = POSPostingMode.PerShift;

    /// <summary>
    /// A shift shortage up to this amount is a company expense; above it, the cashier owes it
    /// (00-Posting-Engine-Architecture.md rule 20). Deliberately a separate figure from
    /// MaxAllowedShiftCashDifference, which only decides whether closing needs approval (rule 19).
    /// </summary>
    public decimal ShiftVarianceEmployeeLiabilityThreshold { get; set; } = 10m;
}

public enum POSPostingMode
{
    /// <summary>An entry with every invoice.</summary>
    PerTransaction = 1,

    /// <summary>One entry when the shift closes.</summary>
    PerShift = 2,

    /// <summary>One entry per branch per day, when someone presses "إقفال اليوم".</summary>
    PerDay = 3
}
