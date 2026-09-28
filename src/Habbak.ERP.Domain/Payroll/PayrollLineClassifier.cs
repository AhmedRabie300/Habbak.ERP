namespace Habbak.ERP.Domain.Payroll;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — the same Earning/Deduction split
/// PayrollCalculationService computes inline (as a local flag, never persisted) and
/// PostPayrollRunCommandHandler re-derives for its per-role posting groups, pulled out once callers
/// needed a THIRD place to agree on it (Payslip.Gross/TotalDeductions generation). Requires
/// <see cref="PayrollLine.SalaryComponent"/> to be loaded (`.Include(l => l.SalaryComponent)`) when
/// <see cref="PayrollLine.SourceType"/> is <see cref="PayrollLineSource.EmployeeSalary"/> — every
/// other source carries no SalaryComponentId and is classified by SourceType alone.
/// </summary>
public static class PayrollLineClassifier
{
    public static bool IsEarning(PayrollLine line) => line.SourceType switch
    {
        PayrollLineSource.Basic or PayrollLineSource.Overtime or PayrollLineSource.Tips => true,
        PayrollLineSource.EmployeeSalary => line.SalaryComponent!.ComponentType == SalaryComponentType.Earning,
        _ => false
    };
}
