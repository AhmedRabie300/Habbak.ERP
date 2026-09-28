using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Domain.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.3 + Docs/Modules/10-Module-HR-Payroll.md
// §2.3 + Docs/Implementation/Phase-4-Research.md §2.3/§2.4. TipsDistribution+TipsDistributionLine
// included here per Phase-4-Research.md §2.4 (moved into Phase 4 scope — rule 39 falls inside the
// rules-28-40 range the master plan assigns to Phase 4, and TipsPayable is the 7th posting template).
// EmployeeAdvance/EmployeePenalty/EmployeeBonus/EmployeeDeduction/EmployeeEndOfService and the rest of
// §2.3's entities stay Phase 5 (Phase-4-Research.md §2.5) — not here.

/// <summary>A reusable payroll-engine catalog entry (basic, transport allowance, meal allowance,
/// incentive, commission, tips…) — §2.3. Distinct from HR.EmploymentContractLine, which is a static
/// per-contract line, not a catalog (Phase-4-Research.md §2.3).</summary>
public class SalaryComponent : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public SalaryComponentType ComponentType { get; set; }
    public CalculationMethod CalculationMethod { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsInsurable { get; set; }
    public bool IsRecurring { get; set; }
    public SalaryComponentSource SourceType { get; set; } = SalaryComponentSource.Fixed;

    /// <summary>Overrides the template's default posting account for this component specifically
    /// (e.g. a meal allowance posted to its own expense account instead of the generic
    /// SalariesExpense) — null means "use the posting template's default role".</summary>
    public CompanyAccountRole? AccountRole { get; set; }
}

/// <summary>A named template of components an employee's pay is built from — §2.3, copied into
/// EmployeeSalary rows at hire time (or a salary change), not read directly by the payroll engine.</summary>
public class SalaryStructure : AuditableEntity, ICompanyScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public ICollection<SalaryStructureLine> Lines { get; set; } = [];
}

/// <summary>One component of a SalaryStructure — exactly one of Amount/Percentage is meaningful per
/// SalaryComponent.CalculationMethod (Fixed uses Amount, PercentOfBasic uses Percentage); both are
/// nullable rather than picking one, since which applies depends on the referenced component.</summary>
public class SalaryStructureLine : AuditableEntity
{
    public long SalaryStructureId { get; set; }
    public SalaryStructure SalaryStructure { get; set; } = null!;

    public long SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public decimal? Amount { get; set; }
    public decimal? Percentage { get; set; }

    public int Order { get; set; }
}

/// <summary>The actual, dated amount an employee is paid for one component — §2.3: "مؤرّخ، فبيغني عن
/// جدول تاريخ منفصل". This is what the payroll engine reads for recurring (Fixed) components; it is
/// filled manually by HR (at hiring or on a salary change), never auto-derived from
/// EmploymentContractLine (Phase-4-Research.md §2.3 — no automatic conversion).</summary>
public class EmployeeSalary : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>One calendar month's payroll window for a company — §2.3, rule 33 (Cutoff/Locked).</summary>
public class PayrollPeriod : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Open;
}

/// <summary>One payroll calculation/approval/posting/payment cycle — §2.3, rule 30 (triple
/// idempotency: this row's IdempotencyKey, PostingKeys.For(...,"PayrollRun.Post",...) and
/// PostingKeys.For(...,"PayrollRun.Pay",...), Phase-4-Research.md §1.3).</summary>
public class PayrollRun : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public PayrollRunType RunType { get; set; }
    public Guid IdempotencyKey { get; set; }
    public long? ReferenceId { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public long? ApprovalInstanceId { get; set; }
    public long? JournalEntryId { get; set; }
    public long? PaymentJournalEntryId { get; set; }

    public int EmployeeCount { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalEmployerCost { get; set; }
}

/// <summary>One component amount for one employee within a PayrollRun — §2.3, rule 29 (RateSnapshot:
/// a JSON string capturing whatever rate/multiplier/bracket actually applied, so a reprinted old
/// payslip never changes even if the legal tables are amended later — same free-text-JSON-column
/// approach as PostingTemplate.TemplateSnapshotJson, Phase-4-Research.md §1.4, serialized manually
/// with System.Text.Json since no HasConversion JSON helper exists in the codebase yet).
/// <see cref="SourceType"/>+<see cref="SourceId"/> point at the record this line was computed from
/// (an OvertimeRequest, a TipsDistributionLine, an EmployeeSalary row, …) — distinct from
/// SalaryComponent.SourceType, which only says what *kind* of source a component uses (§2.3 module
/// doc note, Phase-4-Research.md §1.1).</summary>
public class PayrollLine : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = null!;

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public long? CostCenterDimensionValueId { get; set; }

    public long SalaryComponentId { get; set; }
    public SalaryComponent SalaryComponent { get; set; } = null!;

    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }

    /// <summary>JSON snapshot of the rate/rule actually applied — manually serialized, not a typed
    /// column (Phase-4-Research.md §1.4).</summary>
    public string? RateSnapshot { get; set; }

    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
}

/// <summary>The frozen payslip for one employee in one PayrollRun — §2.3, rule 38 (not visible in
/// self-service until the run is Posted).</summary>
public class Payslip : AuditableEntity, ICompanyScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }

    public long PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = null!;

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public decimal Gross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal Net { get; set; }

    public DateTime IssuedAtUtc { get; set; }
    public string? PdfPath { get; set; }
    public DateTime? ViewedAtUtc { get; set; }
}

/// <summary>A branch's tips pool for one period, split among employees — §2.3/§4.7. Approved
/// distributions fold into PayrollLine rows of the next Regular PayrollRun (rule 39) and close out
/// TipsPayable. Added to Phase 4 (not Phase 5) per Phase-4-Research.md §2.4.</summary>
public class TipsDistribution : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public decimal TotalAmount { get; set; }
    public TipsDistributionMethod Method { get; set; }
    public TipsDistributionStatus Status { get; set; } = TipsDistributionStatus.Draft;
    public long? ApprovalInstanceId { get; set; }

    public ICollection<TipsDistributionLine> Lines { get; set; } = [];
}

/// <summary>One employee's share of a TipsDistribution — same 1:N/no-own-scoping pattern as
/// EmploymentContractLine/PayrollTaxBracket (scoping inherited from the parent via FK).</summary>
public class TipsDistributionLine : AuditableEntity
{
    public long TipsDistributionId { get; set; }
    public TipsDistribution TipsDistribution { get; set; } = null!;

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public decimal Share { get; set; }
    public decimal Amount { get; set; }
}
