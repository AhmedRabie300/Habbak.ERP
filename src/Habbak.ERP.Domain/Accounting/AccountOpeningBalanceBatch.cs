using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

public enum AccountOpeningBalanceStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

/// <summary>
/// أرصدة افتتاحية للحسابات (My Remarks/Remarks2.md, bugs 1.4 and 3.9) — a Master/Detail document,
/// same Draft→Posted→(journal entry via IPostingService) pattern every other document in this
/// codebase already uses (Vouchers, WarehouseDocuments...) rather than the doc's literal
/// "one unified save" reading — this lets a mistake get fixed before the journal entry, which is
/// irreversible once posted, actually gets created.
/// </summary>
public class AccountOpeningBalanceBatch : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public string BatchNumber { get; set; } = null!;
    public DateOnly TransactionDate { get; set; }

    public AccountOpeningBalanceStatus Status { get; set; } = AccountOpeningBalanceStatus.Draft;

    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public string? Notes { get; set; }

    public ICollection<AccountOpeningBalanceLine> Lines { get; set; } = new List<AccountOpeningBalanceLine>();
}

/// <summary>One row per account — Amount is always positive; which side of the journal entry it
/// lands on (Debit/Credit) is resolved from Account.Nature at posting time, exactly like
/// StockTransaction.Quantity is always positive with direction resolved from TransactionType.</summary>
public class AccountOpeningBalanceLine : AuditableEntity
{
    public long AccountOpeningBalanceBatchId { get; set; }
    public AccountOpeningBalanceBatch? Batch { get; set; }

    public int LineNumber { get; set; }

    public long AccountId { get; set; }
    public Account? Account { get; set; }

    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
