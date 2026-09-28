using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// مطابقة خزينة (01-Module-Accounting.md, section 2.5).
/// </summary>
public class CashReconciliation : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long TreasuryAccountId { get; set; }
    public Account TreasuryAccount { get; set; } = default!;

    public DateOnly ReconciliationDate { get; set; }

    public decimal ExpectedBalance { get; set; }
    public decimal ActualBalance { get; set; }
    public decimal DifferenceAmount { get; set; }

    /// <summary>Required when DifferenceAmount != 0.</summary>
    public string? DifferenceReason { get; set; }

    /// <summary>Required before the reconciliation is considered final, even when the difference is zero (rule 19).</summary>
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public ICollection<CashReconciliationDenomination> Denominations { get; set; } = new List<CashReconciliationDenomination>();
}

/// <summary>Optional but recommended breakdown of the physical cash count by denomination.</summary>
public class CashReconciliationDenomination : AuditableEntity
{
    public long CashReconciliationId { get; set; }
    public CashReconciliation CashReconciliation { get; set; } = default!;

    public decimal DenominationValue { get; set; }
    public int Count { get; set; }
}
