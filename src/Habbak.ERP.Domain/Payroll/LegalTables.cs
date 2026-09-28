using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.2 + Docs/Modules/10-Module-HR-Payroll.md
// §2.4 + Docs/Implementation/Phase-4-Research.md §1.4/§2.1/§2.2. Eleven dated legal/policy tables —
// ten from §2.4 (PayrollTaxBracketSet/PayrollTaxBracket counted as two real tables, Phase-4-Research.md
// §2.1) plus EndOfServicePolicy (originally listed under §2.3 "الرواتب" with no owning phase — added
// here per Phase-4-Research.md §2.2, since Phase 4's own monthly-EOS-provision posting template needs
// to read PolicyType). Every row implements IEffectiveDatedEntity (Domain/Common/IEffectiveDatedEntity.cs)
// — none of them is ILookupEntity (a wage/rate isn't a dropdown choice), and none is IBranchScopedEntity
// (company-wide, like HrSettings). All numbers are ⏸️ Pending Legal — confirm with a labour-law
// consultant before entering real values (10-Module-HR-Payroll.md §2.4 header note).

/// <summary>National/company minimum wage — §2.4. <see cref="Sector"/> null means the general
/// (non-sector-specific) minimum wage; a sector-specific row overrides it for that sector's employees.
/// No sector lookup entity exists yet, so this is free text pending the real classification.</summary>
public class MinimumWage : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal Amount { get; set; }
    public string? Sector { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Social insurance contribution rates (employee share + employer share) — §2.4, feeds the
/// "تأمينات العامل" deduction (rule 28) and <c>SocialInsuranceExpense</c>/<c>SocialInsurancePayable</c>
/// posting (§6.2).</summary>
public class SocialInsuranceRate : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal EmployeeRate { get; set; }
    public decimal EmployerRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>The wage floor/ceiling social insurance contributions are computed against — §2.4,
/// "بيتحدثوا كل يناير".</summary>
public class InsurableWageLimit : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal MinWage { get; set; }
    public decimal MaxWage { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>One dated income-tax bracket set (personal exemption + the disqualification rule that
/// applies when total income crosses certain thresholds) — §2.4. The exact disqualification mechanics
/// are ⏸️ Pending Legal, so <see cref="DisqualificationRulesDescription"/> is a free-text placeholder
/// for the accountant/consultant to fill in, not encoded business logic — encoding a rule that isn't
/// confirmed yet would be worse than leaving it descriptive.</summary>
public class PayrollTaxBracketSet : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal PersonalExemption { get; set; }
    public string? DisqualificationRulesDescription { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public ICollection<PayrollTaxBracket> Brackets { get; set; } = [];
}

/// <summary>One bracket line of a <see cref="PayrollTaxBracketSet"/> — same 1:N/no-own-scoping pattern
/// as EmploymentContractLine (HR/EmploymentContractLine.cs): scoping is inherited from the parent set
/// via the FK, not duplicated on every line. <see cref="ToAmount"/> null means "and above" (the top
/// bracket).</summary>
public class PayrollTaxBracket : AuditableEntity
{
    public long PayrollTaxBracketSetId { get; set; }
    public PayrollTaxBracketSet PayrollTaxBracketSet { get; set; } = null!;

    public decimal FromAmount { get; set; }
    public decimal? ToAmount { get; set; }
    public decimal Rate { get; set; }

    /// <summary>Free display/evaluation ordering (sorted by Order then Id) — not unique within the
    /// set, same as EmploymentContractLine.Order/JournalEntryLine.LineNumber.</summary>
    public int Order { get; set; }
}

/// <summary>"صندوق شهداء وأسر ومصابي الحرب والعمليات الحربية" payroll deduction rate — §2.4, rule 28
/// (the last deduction line before net pay).</summary>
public class MartyrsFundRate : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal Rate { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Overtime multiplier per <see cref="OvertimeType"/> — §2.4 decision 2 (legal minimums:
/// Day 1.35×, Night 1.70×, RestDay/PublicHoliday 2×). <see cref="IsLegalMinimum"/> marks the row the
/// legal floor for its type (rule 18: a company policy row for the same type may be higher, never
/// lower, than the row with this flag). <see cref="GrantsSubstituteDay"/> — rule 19, PublicHoliday only
/// in practice, modelled as a flag rather than hardcoded to one type since policy could extend it.
/// Uses the existing Attendance.OvertimeType (Attendance/Enums.cs) — already built in Phase 3
/// specifically anticipating this table (Phase-4-Research.md §1.5), not a new enum.</summary>
public class OvertimeRate : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public OvertimeType OvertimeType { get; set; }
    public decimal Multiplier { get; set; }
    public bool IsLegalMinimum { get; set; }
    public bool GrantsSubstituteDay { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>The real source of annual leave days by service-years/age tier — §2.4, rule 23. Takes
/// precedence over the LeaveType.AnnualDays fallback (Attendance/LeaveEntities.cs) whenever a row
/// matches the employee's LeaveTypeId + years of service + age on the date being evaluated.
/// <see cref="MinServiceYears"/>/<see cref="MinAge"/> null means "no floor on that dimension" (matches
/// everyone). Multiple concurrent rows are expected — one per LeaveTypeId+MinServiceYears+MinAge tier
/// — only rows sharing the exact same tier must not overlap in time.</summary>
public class LeaveEntitlementRule : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public long LeaveTypeId { get; set; }
    public LeaveType LeaveType { get; set; } = null!;

    public int? MinServiceYears { get; set; }
    public int? MinAge { get; set; }
    public decimal Days { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Monthly cap on total penalty+deduction days a single payroll line may take — §2.4, rule 36
/// (the excess carries to next month's run, it is never dropped).</summary>
public class PenaltyDeductionCap : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public decimal MaxDaysPerMonth { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Statutory notice period by service-years tier — §2.4, feeds NoticePay in
/// EmployeeEndOfService (Phase 5). Multiple concurrent rows are expected (one per
/// MinServiceYears tier); only rows sharing the exact same MinServiceYears must not overlap in time.</summary>
public class NoticePeriodRule : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public int MinServiceYears { get; set; }
    public int NoticeDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>Whether the company grants a contractual end-of-service gratuity on top of the statutory
/// components (LeaveCashOut/NoticePay/Compensation) — Appendix أ.2 item 2, ⏸️ Pending Legal, default
/// None. Added to Phase 4's legal tables per Phase-4-Research.md §2.2 (not originally assigned to any
/// phase) because Phase 4's own monthly-EOS-provision posting template (§6.2) needs PolicyType to
/// decide whether to post at all — with PolicyType = None it deliberately posts nothing (the
/// module doc itself: "لو السياسة None، مخصص نهاية الخدمة يبقى غلط محاسبيًا"). EndOfServiceHeir/
/// EmployeeEndOfService (the full computation) stay Phase 5 — this table only carries the policy
/// switch and its parameters.</summary>
public class EndOfServicePolicy : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public EndOfServicePolicyType PolicyType { get; set; } = EndOfServicePolicyType.None;
    public string? FormulaDescription { get; set; }
    public int? MinServiceYears { get; set; }
    public decimal? DaysPerYear { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public enum EndOfServicePolicyType
{
    None = 1,
    Contractual = 2
}

/// <summary>Daily/monthly overtime ceiling — rule 20 names this as its own "جدول مؤرّخ" separate from
/// the nine-plus-one tables §2.4 actually lists by name; added here (Sub-Batch 4.4, not 4.2) because
/// it only became necessary once the engine needed to detect "over the limit" (Phase-4-Research.md
/// §2.6's approved decision). Either bound may be null (no ceiling on that dimension).</summary>
public class OvertimeLimitRule : AuditableEntity, ICompanyScopedEntity, IEffectiveDatedEntity
{
    public long? CompanyId { get; set; }

    public int? MaxMinutesPerDay { get; set; }
    public int? MaxMinutesPerMonth { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
