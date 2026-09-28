using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// تسوية عهدة (01-Module-Accounting.md, section 2.4).
/// </summary>
public class CustodySettlement : AuditableEntity
{
    public long CustodyRegisterId { get; set; }
    public CustodyRegister CustodyRegister { get; set; } = default!;

    public DateOnly SettlementDate { get; set; }

    /// <summary>Positive = returned by the employee, negative = additional amount owed to them.</summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>The entry resulting from this settlement (expense distribution + refund/due difference).</summary>
    public long? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public ICollection<CustodySettlementLine> Lines { get; set; } = new List<CustodySettlementLine>();
}

/// <summary>
/// One expense line of a custody settlement. The sum of all lines for a single settlement
/// cannot exceed the CustodyRegister's original Amount (rule 22).
/// </summary>
public class CustodySettlementLine : AuditableEntity
{
    public long CustodySettlementId { get; set; }
    public CustodySettlement CustodySettlement { get; set; } = default!;

    public long AccountId { get; set; }
    public Account Account { get; set; } = default!;

    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
