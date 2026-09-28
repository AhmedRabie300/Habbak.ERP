using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// تحويل بين خزينة/بنك (01-Module-Accounting.md, section 2.3).
/// </summary>
public class TreasuryTransfer : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long FromTreasuryAccountId { get; set; }
    public Account FromTreasuryAccount { get; set; } = default!;

    public long ToTreasuryAccountId { get; set; }
    public Account ToTreasuryAccount { get; set; } = default!;

    public decimal Amount { get; set; }
    public DateOnly TransferDate { get; set; }

    public TreasuryTransferStatus Status { get; set; } = TreasuryTransferStatus.Draft;

    /// <summary>My Remarks/Remarks2.md, remark 2.4 — this screen was the one document form
    /// missing a Notes field entirely.</summary>
    public string? Notes { get; set; }

    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
