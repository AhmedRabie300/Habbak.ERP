using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// تسجيل عهدة (01-Module-Accounting.md, section 2.4).
/// </summary>
public class CustodyRegister : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long EmployeeId { get; set; }

    /// <summary>
    /// Option A (Phase 1.2, HR-MASTER-PLAN.md §Phase 1.2): a second, optional column set only by manual
    /// linking to a real <c>Employee</c> row — <see cref="EmployeeId"/> above is left exactly as it is,
    /// untouched and unenforced, because its pre-HR values (placeholder numbers) have no real meaning to
    /// convert automatically. Plain column, no FK — same reasoning as <see cref="EmployeeId"/>, so a
    /// custody record never breaks if the employee it was linked to is later removed.
    /// </summary>
    public long? EmployeeIdLinked { get; set; }

    /// <summary>Direct, nullable column: the branch of the employee holding the custody (rule 25).</summary>
    public long? BranchId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly IssueDate { get; set; }

    public CustodyStatus Status { get; set; } = CustodyStatus.Open;

    /// <summary>The initial custody disbursement entry.</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<CustodySettlement> Settlements { get; set; } = new List<CustodySettlement>();
}
