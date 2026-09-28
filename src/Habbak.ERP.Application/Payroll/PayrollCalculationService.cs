using System.Text.Json;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Payroll;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.4 — the calculation half of rule 28's
/// formula (أساسي + بدلات + إضافي + بقشيش − غياب − جزاءات − أقساط سلف − تأمينات − ضريبة − صندوق شهداء).
///
/// Deliberate simplifications, each because the source data or the exact figure genuinely doesn't
/// exist yet (not because the correct answer was skipped for convenience) — documented so a future
/// phase knows exactly what to revisit rather than re-deriving it from scratch:
/// - Penalties/AdvanceInstallment always produce zero lines: EmployeePenalty/EmployeeAdvance are
///   Phase 5 (Phase-4-Research.md §2.5) — there is nothing to sum yet.
/// - Basic is prorated for partial employment (hire mid-period) and unpaid leave (rule 34); allowance
///   (EmployeeSalary) lines are NOT prorated — paid at their configured amount regardless of partial
///   employment. Real payroll practice varies by company on this; prorating everything uniformly
///   without a company-level policy to confirm which allowances follow which rule would be guessing.
/// - Tax (rule 37): standard "annualize this month × 12, apply brackets, divide by 12" withholding
///   method, feeding EmployeeTaxProfile's YTD totals for the eventual AnnualTaxSettlement true-up.
///   Taxable base = Basic + allowance lines whose SalaryComponent.IsTaxable + Overtime (overtime is
///   assumed taxable — no per-request override exists). Skipped entirely (no line) if no
///   PayrollTaxBracketSet is configured for the date — matches the project's "Pending Legal numbers
///   don't block development" pattern used throughout the legal tables.
/// - Martyrs fund base reuses the same taxable base as tax, for lack of a separately documented one.
/// - Compliance check "Active employee with no insurance registration" (rule 35) is NOT one of the
///   exceptions detected here — EmployeeSocialInsurance (the entity that would answer it) is Phase 5.
/// - Rule 19 (public-holiday overtime granting a substitute leave day) is not wired here — it needs a
///   target LeaveType no company is guaranteed to have configured; OvertimeRate.GrantsSubstituteDay is
///   stored and available for a later phase to consume.
/// </summary>
public sealed class PayrollCalculationService(IApplicationDbContext db)
{
    private static readonly JsonSerializerOptions SnapshotOptions = new();

    public async Task<PayrollCalculationResult> CalculateAsync(PayrollRun run, PayrollPeriod period, CancellationToken cancellationToken)
    {
        var companyId = run.CompanyId;
        var asOf = period.EndDate;

        // Existing lines are replaced wholesale on every (re)calculation — the caller only invokes
        // this while the run is Draft/Calculated (never against a posted run), so there is nothing to
        // preserve (rule: "مفيش إعادة حساب لتشغيل مرحّل").
        var oldLines = await db.PayrollLines.Where(l => l.PayrollRunId == run.Id).ToListAsync(cancellationToken);
        db.PayrollLines.RemoveRange(oldLines);

        var hrSettings = await db.HrSettingsRows.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);
        var monthBasisDays = hrSettings?.MonthBasis == HrMonthBasis.Thirty ? 30 : period.EndDate.DayNumber - period.StartDate.DayNumber + 1;

        var socialInsuranceRates = await db.SocialInsuranceRates.AsNoTracking().Where(r => r.CompanyId == companyId).ToListAsync(cancellationToken);
        var insuranceLimits = await db.InsurableWageLimits.AsNoTracking().Where(r => r.CompanyId == companyId).ToListAsync(cancellationToken);
        var overtimeRates = await db.OvertimeRates.AsNoTracking().Where(r => r.CompanyId == companyId).ToListAsync(cancellationToken);
        var martyrsFundRates = await db.MartyrsFundRates.AsNoTracking().Where(r => r.CompanyId == companyId).ToListAsync(cancellationToken);
        var minimumWages = await db.MinimumWages.AsNoTracking().Where(r => r.CompanyId == companyId && r.Sector == null).ToListAsync(cancellationToken);
        var taxBracketSets = await db.PayrollTaxBracketSets.AsNoTracking().Include(s => s.Brackets)
            .Where(s => s.CompanyId == companyId).ToListAsync(cancellationToken);

        var socialInsuranceRate = EffectiveDateRules.ActiveAsOf(socialInsuranceRates, asOf);
        var insuranceLimit = EffectiveDateRules.ActiveAsOf(insuranceLimits, asOf);
        var martyrsFundRate = EffectiveDateRules.ActiveAsOf(martyrsFundRates, asOf);
        var minimumWage = EffectiveDateRules.ActiveAsOf(minimumWages, asOf);
        var taxBracketSet = EffectiveDateRules.ActiveAsOf(taxBracketSets, asOf);

        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.CompanyId == companyId && (e.Status == EmployeeStatus.Active || e.Status == EmployeeStatus.OnLeave))
            .ToListAsync(cancellationToken);

        var lines = new List<PayrollLine>();
        var exceptions = new List<PayrollExceptionDto>();
        var touchedTipsDistributionIds = new HashSet<long>();
        decimal totalGross = 0, totalDeductions = 0, totalEmployerCost = 0;
        var employeeCount = 0;

        foreach (var employee in employees)
        {
            var contract = await db.EmploymentContracts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.EmployeeId == employee.Id && c.Status == EmploymentContractStatus.Active, cancellationToken);
            if (contract is null)
            {
                continue; // No active contract — nothing to base a salary on, silently excluded (not an exception: the employee simply isn't payroll-eligible this run).
            }

            var entitledStart = employee.HireDate > period.StartDate ? employee.HireDate : period.StartDate;
            if (entitledStart > period.EndDate)
            {
                continue; // Hired after this period ended.
            }
            var entitledDays = period.EndDate.DayNumber - entitledStart.DayNumber + 1;

            var unpaidLeaveDays = await UnpaidLeaveDaysAsync(employee.Id, entitledStart, period.EndDate, cancellationToken);
            var effectiveDays = Math.Max(0, entitledDays - unpaidLeaveDays);

            var employeeLines = new List<(PayrollLine Line, bool IsEarning)>();

            // Basic (PayrollLineSource.Basic, rule 34 proration).
            var basicAmount = Math.Round(contract.BasicSalary * effectiveDays / monthBasisDays, 2);
            employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.Basic, contract.Id, basicAmount,
                new { effectiveDays, monthBasisDays, contract.BasicSalary }, isEarning: true));

            // Allowances/recurring components — full configured amount, not prorated (see class doc).
            var salaryRows = await db.EmployeeSalaries.AsNoTracking().Include(s => s.SalaryComponent)
                .Where(s => s.EmployeeId == employee.Id).ToListAsync(cancellationToken);
            var activeSalaryRows = salaryRows.Where(s => EffectiveDateRules.IsActiveOn(s.EffectiveFrom, s.EffectiveTo, asOf)).ToList();
            decimal taxableEarnings = 0;
            foreach (var row in activeSalaryRows)
            {
                var isEarningComponent = row.SalaryComponent.ComponentType == SalaryComponentType.Earning;
                employeeLines.Add(NewLine(run, employee, row.SalaryComponentId, PayrollLineSource.EmployeeSalary, row.Id, row.Amount, null, isEarning: isEarningComponent));
                if (isEarningComponent && row.SalaryComponent.IsTaxable)
                {
                    taxableEarnings += row.Amount;
                }
            }
            if (contract.BasicSalary > 0)
            {
                taxableEarnings += basicAmount; // Basic is always taxable — no per-employee exemption flag exists on the contract itself.
            }

            // Overtime — approved requests only (rule 17), grouped by OvertimeType (rule 21), excluding
            // any request still blocked on the over-limit HR override (§2.6/Sub-Batch 4.4 decision).
            var overtimeGroups = await db.OvertimeRequests.AsNoTracking()
                .Where(o => o.EmployeeId == employee.Id && o.Status == HrRequestStatus.Approved
                            && o.WorkDate >= period.StartDate && o.WorkDate <= period.EndDate
                            && (!o.ExceedsLimit || o.HrOverrideApprovedByUserId != null))
                .GroupBy(o => o.OvertimeType)
                .Select(g => new { OvertimeType = g.Key, Minutes = g.Sum(o => o.ActualMinutes ?? o.PlannedMinutes) })
                .ToListAsync(cancellationToken);

            decimal overtimeTotal = 0;
            foreach (var group in overtimeGroups)
            {
                var rate = EffectiveDateRules.ActiveAsOf(overtimeRates.Where(r => r.OvertimeType == group.OvertimeType).ToList(), asOf);
                if (rate is null || group.Minutes <= 0 || contract.WorkingHoursPerDay <= 0)
                {
                    continue;
                }
                var hourlyRate = contract.BasicSalary / (monthBasisDays * contract.WorkingHoursPerDay);
                var amount = Math.Round(group.Minutes / 60m * hourlyRate * rate.Multiplier, 2);
                overtimeTotal += amount;
                employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.Overtime, rate.Id, amount,
                    new { group.OvertimeType, group.Minutes, hourlyRate, rate.Multiplier }, isEarning: true, quantity: group.Minutes));
            }
            taxableEarnings += overtimeTotal;

            // Tips — approved distributions for this exact period (rule 39).
            var tipLines = await db.TipsDistributionLines.AsNoTracking().Include(l => l.TipsDistribution)
                .Where(l => l.EmployeeId == employee.Id && l.TipsDistribution.CompanyId == companyId
                            && l.TipsDistribution.PayrollPeriodId == period.Id && l.TipsDistribution.Status == TipsDistributionStatus.Approved)
                .ToListAsync(cancellationToken);
            foreach (var tipLine in tipLines)
            {
                employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.Tips, tipLine.Id, tipLine.Amount, null, isEarning: true));
                touchedTipsDistributionIds.Add(tipLine.TipsDistributionId);
            }

            // Absence (rule 28's explicit "− غياب" term — separate from the entitled-days proration above).
            var absentDays = await db.Attendances.CountAsync(
                a => a.EmployeeId == employee.Id && a.WorkDate >= entitledStart && a.WorkDate <= period.EndDate && a.Status == AttendanceStatus.Absent,
                cancellationToken);
            decimal absenceAmount = 0;
            if (absentDays > 0)
            {
                absenceAmount = Math.Round(contract.BasicSalary / monthBasisDays * absentDays, 2);
                employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.Absence, null, absenceAmount, new { absentDays, monthBasisDays }, isEarning: false));
            }

            // Social insurance (employee share) — company-wide legal rate, capped wage.
            decimal socialInsuranceAmount = 0, employerSocialInsuranceAmount = 0;
            if (socialInsuranceRate is not null)
            {
                var cappedWage = contract.InsurableWage;
                if (insuranceLimit is not null)
                {
                    cappedWage = Math.Clamp(cappedWage, insuranceLimit.MinWage, insuranceLimit.MaxWage);
                }
                socialInsuranceAmount = Math.Round(cappedWage * socialInsuranceRate.EmployeeRate, 2);
                employerSocialInsuranceAmount = Math.Round(cappedWage * socialInsuranceRate.EmployerRate, 2);
                employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.LegalSocialInsurance, socialInsuranceRate.Id, socialInsuranceAmount,
                    new { cappedWage, socialInsuranceRate.EmployeeRate }, isEarning: false));
            }

            // Tax (rule 37) — annualize-and-divide withholding against EmployeeTaxProfile's YTD totals.
            decimal taxAmount = 0;
            var taxProfile = await GetOrCreateTaxProfileAsync(employee.Id, companyId, period.Year, cancellationToken);
            if (!taxProfile.IsExempt && taxBracketSet is not null)
            {
                var annualized = taxableEarnings * 12;
                var afterExemption = Math.Max(0, annualized - taxBracketSet.PersonalExemption);
                var annualTax = ApplyBrackets(afterExemption, taxBracketSet.Brackets);
                taxAmount = Math.Round(annualTax / 12, 2);
                taxProfile.YtdTaxableIncome += taxableEarnings;
                taxProfile.YtdTaxWithheld += taxAmount;
                if (taxAmount > 0)
                {
                    employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.LegalTax, taxBracketSet.Id, taxAmount,
                        new { taxableEarnings, annualized, taxBracketSet.PersonalExemption, afterExemption, annualTax }, isEarning: false));
                }
            }

            // Martyrs fund (rule 28's last deduction term) — same taxable base as tax, for lack of a
            // separately documented one (⏸️ Pending Legal, like every rate in these tables).
            decimal martyrsFundAmount = 0;
            if (martyrsFundRate is not null)
            {
                martyrsFundAmount = Math.Round(taxableEarnings * martyrsFundRate.Rate, 2);
                if (martyrsFundAmount > 0)
                {
                    employeeLines.Add(NewLine(run, employee, null, PayrollLineSource.LegalMartyrsFund, martyrsFundRate.Id, martyrsFundAmount,
                        new { taxableEarnings, martyrsFundRate.Rate }, isEarning: false));
                }
            }

            var gross = employeeLines.Where(l => l.IsEarning).Sum(l => l.Line.Amount);
            var deductions = employeeLines.Where(l => !l.IsEarning).Sum(l => l.Line.Amount);
            var net = gross - deductions;

            lines.AddRange(employeeLines.Select(l => l.Line));
            totalGross += gross;
            totalDeductions += deductions;
            totalEmployerCost += gross + employerSocialInsuranceAmount;
            employeeCount++;

            DetectExceptions(employee, net, minimumWage, exceptions);
        }

        foreach (var tipsDistributionId in touchedTipsDistributionIds)
        {
            var distribution = await db.TipsDistributions.FirstAsync(t => t.Id == tipsDistributionId, cancellationToken);
            distribution.Status = TipsDistributionStatus.Included;
        }

        db.PayrollLines.AddRange(lines);
        run.EmployeeCount = employeeCount;
        run.TotalGross = totalGross;
        run.TotalDeductions = totalDeductions;
        run.TotalNet = totalGross - totalDeductions;
        run.TotalEmployerCost = totalEmployerCost;
        run.Status = PayrollRunStatus.Calculated;

        return new PayrollCalculationResult(employeeCount, run.TotalNet, exceptions);
    }

    /// <summary>
    /// Deliberately never sets the <c>SalaryComponent</c> navigation property, even when
    /// <paramref name="salaryComponentId"/> is known: these PayrollLine objects get AddRange'd while
    /// still detached, and pointing the navigation at an AsNoTracking-loaded SalaryComponent would
    /// pull that already-persisted row into the same Added graph, and EF would try to re-insert it as
    /// a duplicate. Callers that need the component's ComponentType (to sort a line into gross vs.
    /// deductions) read it themselves before calling this and pass the result back via
    /// <paramref name="isEarning"/> instead.
    /// </summary>
    private static (PayrollLine Line, bool IsEarning) NewLine(
        PayrollRun run, Employee employee, long? salaryComponentId, PayrollLineSource source, long? sourceId, decimal amount, object? snapshot,
        bool isEarning, decimal? quantity = null)
    {
        var line = new PayrollLine
        {
            CompanyId = run.CompanyId,
            BranchId = employee.BranchId,
            PayrollRunId = run.Id,
            EmployeeId = employee.Id,
            CostCenterDimensionValueId = employee.CostCenterDimensionValueId,
            SalaryComponentId = salaryComponentId,
            Amount = amount,
            Quantity = quantity,
            RateSnapshot = snapshot is null ? null : JsonSerializer.Serialize(snapshot, SnapshotOptions),
            SourceType = source,
            SourceId = sourceId
        };
        return (line, isEarning);
    }

    private async Task<int> UnpaidLeaveDaysAsync(long employeeId, DateOnly windowStart, DateOnly windowEnd, CancellationToken cancellationToken)
    {
        var unpaidRequests = await db.LeaveRequests.AsNoTracking().Include(r => r.LeaveType)
            .Where(r => r.EmployeeId == employeeId && r.Status == HrRequestStatus.Approved && !r.LeaveType.IsPaid
                        && r.StartDate <= windowEnd && r.EndDate >= windowStart)
            .ToListAsync(cancellationToken);

        var days = 0;
        foreach (var request in unpaidRequests)
        {
            var overlapStart = request.StartDate > windowStart ? request.StartDate : windowStart;
            var overlapEnd = request.EndDate < windowEnd ? request.EndDate : windowEnd;
            if (overlapEnd >= overlapStart)
            {
                days += overlapEnd.DayNumber - overlapStart.DayNumber + 1;
            }
        }
        return days;
    }

    private async Task<EmployeeTaxProfile> GetOrCreateTaxProfileAsync(long employeeId, long? companyId, int taxYear, CancellationToken cancellationToken)
    {
        var profile = await db.EmployeeTaxProfiles.FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.TaxYear == taxYear, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new EmployeeTaxProfile { CompanyId = companyId, EmployeeId = employeeId, TaxYear = taxYear };
        db.EmployeeTaxProfiles.Add(profile);
        return profile;
    }

    private static decimal ApplyBrackets(decimal taxableAmount, IEnumerable<PayrollTaxBracket> brackets)
    {
        decimal tax = 0;
        foreach (var bracket in brackets.OrderBy(b => b.FromAmount))
        {
            if (taxableAmount <= bracket.FromAmount)
            {
                continue;
            }
            var upper = bracket.ToAmount ?? taxableAmount;
            var amountInBracket = Math.Min(taxableAmount, upper) - bracket.FromAmount;
            if (amountInBracket > 0)
            {
                tax += amountInBracket * bracket.Rate;
            }
        }
        return tax;
    }

    /// <summary>Rule 35's exception review, minus the insurance-registration check (needs
    /// EmployeeSocialInsurance, Phase 5) and the "sudden change vs last month" check (no confirmed
    /// threshold exists anywhere in the docs — ⏸️ Pending Company, not guessed here).</summary>
    private static void DetectExceptions(Employee employee, decimal net, MinimumWage? minimumWage, List<PayrollExceptionDto> exceptions)
    {
        if (net < 0)
        {
            exceptions.Add(new PayrollExceptionDto(employee.Id, "صافي سالب"));
        }
        if (employee.EmploymentType == EmploymentType.FullTime && minimumWage is not null && net < minimumWage.Amount)
        {
            exceptions.Add(new PayrollExceptionDto(employee.Id, "صافي تحت الحد الأدنى للأجور"));
        }
    }
}

public sealed record PayrollExceptionDto(long EmployeeId, string ReasonAr);

public sealed record PayrollCalculationResult(int EmployeeCount, decimal TotalNet, IReadOnlyList<PayrollExceptionDto> Exceptions);
