using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>تسكين وردية (05-Module-POS-Shifts.md, section 2.1) — يحدد مين مسموح له يفتح وردية على
/// أي نقطة بيع في أي يوم (قاعدة 31).</summary>
public class ShiftAssignment : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long UserId { get; set; }

    public DateOnly AssignedDate { get; set; }
}
