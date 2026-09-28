namespace Habbak.ERP.Domain.Payroll;

// Docs/Modules/10-Module-HR-Payroll.md §2.6 (canonical enum list) + §2.3. PayrollRunType/
// PayrollRunStatus/SalaryComponentSource are copied here exactly as §2.6 defines them — this is
// their first real implementation (Phase-4-Research.md §1.1: zero payroll domain code existed before
// Sub-Batch 4.2/4.3).

/// <summary>Mirrors HR.ContractLineType (Earning/Deduction) in shape, but deliberately a separate
/// enum: a SalaryComponent is a reusable payroll-engine catalog entry, an EmploymentContractLine is a
/// static per-contract amount (HR/EmploymentContractLine.cs, Phase-4-Research.md §2.3 — the two stay
/// decoupled, no automatic conversion between them). Coupling the two enums would wire Domain.Payroll
/// to Domain.HR for no reason beyond both being an earning/deduction dichotomy.</summary>
public enum SalaryComponentType { Earning = 1, Deduction = 2 }

public enum CalculationMethod { Fixed = 1, PercentOfBasic = 2, Hourly = 3, Formula = 4 }

/// <summary>§2.3 — where the amount comes from when <c>IsRecurring = false</c>; recurring (Fixed)
/// components come from EmployeeSalary instead. §2.8 of Phase-4-Research.md: FromCommissions is
/// schema-ready only — no sales-rep/collected-invoice mechanism exists yet to compute it.</summary>
public enum SalaryComponentSource { Fixed = 1, FromOvertime = 2, FromTips = 3, FromCommissions = 4, FromAdvances = 5, FromPenalties = 6 }

public enum PayrollPeriodStatus { Open = 1, Locked = 2, Closed = 3 }

public enum PayrollRunType { Regular = 1, Supplementary = 2, FinalSettlement = 3, AnnualTaxSettlement = 4 }

public enum PayrollRunStatus { Draft = 1, Calculated = 2, PendingApproval = 3, Approved = 4, Posted = 5, Paid = 6, Reversed = 7, Rejected = 8 }

/// <summary>What a PayrollLine was computed from — distinct from SalaryComponentSource, which only
/// describes a SalaryComponent's own configured source (§2.3 module doc note). Added 2026-09-28
/// (HR-MASTER-PLAN.md §Phase 4 Amendments Log) alongside PayrollLine.SalaryComponentId becoming
/// nullable: the three Legal* values are exactly the lines that carry no SalaryComponentId. Every
/// other value corresponds to a line that does carry one. Penalty/AdvanceInstallment are schema-ready
/// only — EmployeePenalty/AdvanceInstallment are Phase 5, so no line uses these values until then
/// (Phase-4-Research.md §2.5's "schema-ready" pattern, same as SourceModule.Payroll before Phase 4).</summary>
public enum PayrollLineSource
{
    EmployeeSalary = 1,
    Overtime = 2,
    Tips = 3,
    Penalty = 4,
    AdvanceInstallment = 5,
    Absence = 6,
    LegalSocialInsurance = 7,
    LegalTax = 8,
    LegalMartyrsFund = 9,
    PriorPeriodAdjustment = 10,

    /// <summary>EmploymentContract.BasicSalary itself — added in Sub-Batch 4.4 once the engine needed
    /// a source for it. Not "EmployeeSalary" because Basic isn't a SalaryComponent-tied row; carries
    /// no SalaryComponentId, same as the Legal* values.</summary>
    Basic = 11,

    /// <summary>The employer's own social insurance share (§6.2's SocialInsuranceExpense — distinct
    /// from LegalSocialInsurance, which is the employee's withheld share). Added in Sub-Batch 4.5 so
    /// the posting command can aggregate it per employee/cost-center straight from PayrollLine, instead
    /// of re-deriving cappedWage×EmployerRate a second time outside PayrollCalculationService. Never
    /// counted in an employee's own Gross/Deductions/Net (it isn't part of their pay) — only in
    /// PayrollRun.TotalEmployerCost and the accrual posting.</summary>
    EmployerSocialInsurance = 12
}

/// <summary>§2.3/§4.7 — Included (line 128/327 of the module doc) is reached once the distribution's
/// lines have been folded into a PayrollRun as PayrollLine rows; there is no Cancelled state in the
/// documented lifecycle (§4.7), so none is added here.</summary>
public enum TipsDistributionMethod { Equal = 1, ByDays = 2, ByHours = 3, ByPoints = 4 }

public enum TipsDistributionStatus { Draft = 1, Pending = 2, Approved = 3, Included = 4, Rejected = 5 }
