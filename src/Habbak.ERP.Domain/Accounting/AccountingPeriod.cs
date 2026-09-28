using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// الفترة المالية (01-Module-Accounting.md, section 2.6).
/// </summary>
public class AccountingPeriod : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;

    public long? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<PeriodCloseChecklistItem> ChecklistItems { get; set; } = new List<PeriodCloseChecklistItem>();
}

/// <summary>One row per closing condition (00-Project-Overview.md conventions apply; keys are fixed, see PeriodCloseChecklistItemKey).</summary>
public class PeriodCloseChecklistItem : AuditableEntity
{
    public long PeriodId { get; set; }
    public AccountingPeriod Period { get; set; } = default!;

    public PeriodCloseChecklistItemKey ItemKey { get; set; }
    public bool IsSatisfied { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
}
