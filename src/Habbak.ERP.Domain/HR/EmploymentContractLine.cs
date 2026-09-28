using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Docs/Implementation/Phase-3C-Research.md §3.1 — allowance/deduction line on top of
/// EmploymentContract.BasicSalary (e.g. "بدل سكن"), not a payroll calculation engine (that's the
/// future Phase 4 SalaryComponent). Same 1:N/Replace-All pattern as BranchRequestLine: no
/// ICompanyScopedEntity/IBranchScopedEntity of its own — scoping is inherited from the parent
/// EmploymentContract via the FK, not duplicated on every line.
/// </summary>
public class EmploymentContractLine : AuditableEntity
{
    public long EmploymentContractId { get; set; }
    public EmploymentContract EmploymentContract { get; set; } = null!;

    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public decimal Amount { get; set; }
    public ContractLineType Type { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsInsurable { get; set; }

    /// <summary>Free display ordering (sorted by Order then Id) — not unique within the contract,
    /// same as JournalEntryLine.LineNumber.</summary>
    public int Order { get; set; }
}
