using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>جدول الحقول بالمواصفة يعرّف 3 حالات بس (من غير Draft) — بعكس SalesReturn (Draft →
/// Posted)، مرتجع POS بيتسجل ويترحّل فوريًا في نفس الفعل، نفس مبدأ POSInvoice (قاعدة 15: "تُنشأ
/// Posted مباشرة"). Rejected/Cancelled مُعلَنتان لاكتمال المخطط فقط — غير قابلتين للوصول في هذا
/// البناء (مفيش مسار إبطال إداري مبني بعد)، نفس نمط POSInvoiceStatus.Voided.</summary>
public enum POSReturnStatus
{
    Posted = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>
/// مرتجع نقطة بيع (05-Module-POS-Shifts.md, section 2.3، قاعدة 16) — SourceInvoiceId إلزامي دايمًا
/// (بعكس SalesReturn اللي بيسيبه اختياري). بيرجّع كمية المخزون فورًا (TransactionType.POSReturn)
/// لمخزن POSTerminal.DefaultWarehouseId. لا يغيّر حالة POSInvoice المصدر — نفس قرار SalesReturn
/// الفعلي اللي ما بيلمسش SalesInvoice.Status خالص، حفاظًا على الاتساق بين الموديولين. لا يوجد
/// JournalEntryId فعلي (مؤجَّل، نفس كل مستندات الموديول).
/// </summary>
public class POSReturn : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long ShiftId { get; set; }
    public Shift? Shift { get; set; }

    public long SourceInvoiceId { get; set; }
    public POSInvoice? SourceInvoice { get; set; }

    public string ReturnNumber { get; set; } = null!;
    public DateOnly ReturnDate { get; set; }
    public string Reason { get; set; } = null!;

    public POSReturnStatus Status { get; set; } = POSReturnStatus.Posted;
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<POSReturnLine> Lines { get; set; } = new List<POSReturnLine>();
}

public class POSReturnLine : AuditableEntity
{
    public long POSReturnId { get; set; }
    public POSReturn? POSReturn { get; set; }

    public int LineNumber { get; set; }
    public long ItemId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
