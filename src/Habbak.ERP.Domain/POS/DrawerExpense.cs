using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>
/// مصروف درج (05-Module-POS-Shifts.md, section 3، قاعدة 33) — مصروف نقدي مسجَّل مباشرة أثناء
/// الوردية، يتطلب ExpenseAccountId إلزاميًا. القاعدة تنص على إنشاء قيد محاسبي عبر IPostingService
/// وقت التسجيل — مؤجَّل هنا عمدًا (JournalEntryId فاضل null) بنفس الاتساق المتّبع مع كل مستندات
/// الموديول لحد الآن (مفيش IPostingService متكامل في أي حاجة حتى الآن). لا يوجد سير اعتماد هنا
/// (بعكس DrawerMovement، قاعدة 33 ما ذكرتش اعتماد) ولا تعديل/حذف بعد التسجيل — سجل تدقيق نقدي ثابت.
/// </summary>
public class DrawerExpense : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public long ExpenseAccountId { get; set; }
    public Account? ExpenseAccount { get; set; }

    public decimal Amount { get; set; }
    public string Description { get; set; } = null!;

    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
