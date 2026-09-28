using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>حالة الوردية (05-Module-POS-Shifts.md, section 4.1). لا يوجد مسار "رفض نهائي" هنا
/// بالمعنى القياسي (استثناء موثَّق صراحة، قاعدة 26) — الوردية دايمًا لازم تنتهي بإغلاق فعلي
/// موثَّق؛ Rejected مُعلَنة لاكتمال المخطط فقط حسب جدول المواصفة، غير قابلة للوصول في هذا البناء
/// (لا يوجد محرك موافقات حقيقي مربوط بعد — PendingCloseApproval تُعتمَد يدويًا بانتقال مباشر
/// لـClosed، نفس نمط ApprovalInstanceId غير المُفعَّل في كل الموديولات السابقة).</summary>
public enum ShiftStatus
{
    Open = 1,
    PendingCloseApproval = 2,
    Closed = 3,
    Rejected = 4,
    Cancelled = 5
}

/// <summary>
/// وردية (05-Module-POS-Shifts.md, section 2.1) — screen #1/#3/#14. لا يوجد `UserId` مرجعي حقيقي
/// في هذا الكود بعد (موديول شئون العاملين لسه مبنيش) — CashierUserId/ClosedByUserId حقول `long`
/// مجردة بدون FK/Navigation، بنفس نمط التأجيل المُتَّبَع فعليًا مع CustodyOfficer.EmployeeId.
/// </summary>
public class Shift : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long CashierUserId { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    /// <summary>محسوبة من مجموع ShiftDenominationCount نوع Opening (قاعدة 1).</summary>
    public decimal OpeningCashAmount { get; set; }

    /// <summary>OpeningCashAmount + مبيعات نقدية − سحوبات معتمدة + إيداعات معتمدة (بالسالب) −
    /// مصروفات الدرج (قاعدة 6) — تُحسب لحظة الإغلاق.</summary>
    public decimal? ExpectedClosingCashAmount { get; set; }

    /// <summary>محسوبة من مجموع ShiftDenominationCount نوع Closing.</summary>
    public decimal? ActualClosingCashAmount { get; set; }

    public decimal? DifferenceAmount { get; set; }

    public long? ClosedByUserId { get; set; }

    /// <summary>مُعلَن لاكتمال المخطط — لا يوجد محرك موافقات حقيقي مربوط بعد (نفس نمط
    /// ApprovalInstanceId في كل الكيانات السابقة).</summary>
    public long? ApprovalInstanceId { get; set; }

    /// <summary>The entry for the cash difference counted at close — separate from the sales entry, in every posting mode.</summary>
    public long? VarianceJournalEntryId { get; set; }
    public JournalEntry? VarianceJournalEntry { get; set; }

    public ICollection<ShiftDenominationCount> DenominationCounts { get; set; } = new List<ShiftDenominationCount>();
}

public enum DenominationCountType
{
    Opening = 1,
    Closing = 2
}

public class ShiftDenominationCount : AuditableEntity
{
    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public DenominationCountType CountType { get; set; }
    public decimal DenominationValue { get; set; }
    public int Count { get; set; }
}
