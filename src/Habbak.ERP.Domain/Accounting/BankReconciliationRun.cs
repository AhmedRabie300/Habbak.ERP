using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// مطابقة بنك (01-Module-Accounting.md, section 2.5).
/// </summary>
public class BankReconciliationRun : AuditableEntity
{
    public long BankAccountId { get; set; }
    public Account BankAccount { get; set; } = default!;

    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public BankReconciliationStatus Status { get; set; } = BankReconciliationStatus.InProgress;

    public DateTime? BankStatementImportDate { get; set; }
    public long? ImportedByUserId { get; set; }

    /// <summary>Set once the reconciliation is approved with a difference to adjust.</summary>
    public long? AdjustmentJournalEntryId { get; set; }
    public JournalEntry? AdjustmentJournalEntry { get; set; }

    public ICollection<BankReconciliationLine> Lines { get; set; } = new List<BankReconciliationLine>();
}

/// <summary>
/// One matched line: SystemTransactionId/BankStatementLineId cannot both be null (rule 23),
/// and SystemTransactionType is required whenever SystemTransactionId is set.
/// </summary>
public class BankReconciliationLine : AuditableEntity
{
    public long BankReconciliationRunId { get; set; }
    public BankReconciliationRun BankReconciliationRun { get; set; } = default!;

    public SystemTransactionType? SystemTransactionType { get; set; }
    public long? SystemTransactionId { get; set; }

    public long? BankStatementLineId { get; set; }

    public decimal MatchedAmount { get; set; }
    public bool IsAutoMatched { get; set; }
}
