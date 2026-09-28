using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Payroll;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.5 — Approved→Posted (accrual)→Paid
// (payment), the two halves of PostingScreenCatalog's "PAY_PAYROLL_RUNS" screen (Stage-gated
// templates). Rule 31's third idempotency key (the calculation key from Sub-Batch 4.4 is the first)
// comes from PostingKeys.For itself — the same "{Entity}.{Action}" convention as
// "DepreciationRun.Post" (Phase-4-Research.md §1.3).

public sealed record PostPayrollRunCommand(long PayrollRunId) : IRequest;

public sealed class PostPayrollRunCommandValidator : AbstractValidator<PostPayrollRunCommand>
{
    public PostPayrollRunCommandValidator() => RuleFor(x => x.PayrollRunId).GreaterThan(0);
}

public sealed class PostPayrollRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IPostingTemplateEngine posting)
    : IRequestHandler<PostPayrollRunCommand>
{
    private static readonly CompanyAccountRole[] AccrualRoles =
    [
        CompanyAccountRole.SalariesExpense, CompanyAccountRole.SocialInsuranceExpense,
        CompanyAccountRole.SalariesPayable, CompanyAccountRole.SocialInsurancePayable,
        CompanyAccountRole.PayrollTaxPayable, CompanyAccountRole.MartyrsFundPayable
    ];

    public async Task Handle(PostPayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        if (run.Status != PayrollRunStatus.Approved)
        {
            throw new BusinessRuleException("PAY-RUN-NOT-APPROVED", "لازم يكون التشغيل معتمد قبل الترحيل.");
        }

        var lines = await db.PayrollLines.AsNoTracking().Include(l => l.SalaryComponent)
            .Where(l => l.PayrollRunId == run.Id).ToListAsync(cancellationToken);

        var employeeIds = lines.Select(l => l.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);

        var groups = await BuildAccrualGroupsAsync(lines, employees, cancellationToken);
        var tipsAmount = lines.Where(l => l.SourceType == PayrollLineSource.Tips).Sum(l => l.Amount);

        var result = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = current.CompanyId,
            ScreenCode = PostingScreenCatalog.PayrollRun,
            SourceModule = SourceModule.Payroll,
            SourceDocumentType = SourceDocumentType.Payroll,
            SourceDocumentId = run.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = $"استحقاق رواتب — تشغيل رقم {run.Id}",
            IdempotencyKey = PostingKeys.For(current.CompanyId, "PayrollRun.Post", run.Id),
            Context = PostingContext.Create(
                fields: new Dictionary<string, object?> { ["Stage"] = "Accrual", ["TipsPayableAmount"] = tipsAmount, ["TotalNet"] = run.TotalNet },
                groups: groups)
        }, cancellationToken);

        run.JournalEntryId = result?.Id;
        run.Status = PayrollRunStatus.Posted;

        // Rule 38 — the frozen payslip only becomes visible in self-service once Posted; generated
        // here (once, guarded by the Approved-only status check above) rather than at Calculate time,
        // so a pre-approval recompute (Draft/Calculated loop, Sub-Batch 4.4) never has to reconcile a
        // stale Payslip against regenerated PayrollLines.
        var issuedAt = DateTime.UtcNow;
        // EmployerSocialInsurance is excluded — it's the employer's own cost (PayrollRun.TotalEmployerCost),
        // never part of the employee's own Gross/Deductions/Net (PayrollCalculationService's own rule).
        var employeeOwnLines = lines.Where(l => l.SourceType != PayrollLineSource.EmployerSocialInsurance);
        foreach (var group in employeeOwnLines.GroupBy(l => l.EmployeeId))
        {
            var gross = group.Where(PayrollLineClassifier.IsEarning).Sum(l => l.Amount);
            var deductions = group.Where(l => !PayrollLineClassifier.IsEarning(l)).Sum(l => l.Amount);
            db.Payslips.Add(new Payslip
            {
                CompanyId = run.CompanyId,
                PayrollRunId = run.Id,
                EmployeeId = group.Key,
                Gross = gross,
                TotalDeductions = deductions,
                Net = gross - deductions,
                IssuedAtUtc = issuedAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Builds one PostingGroupItem per (role, employee), aggregated from PayrollLine straight — no
    /// business logic is re-derived here, only re-classified into which of the six accrual roles each
    /// already-computed line belongs to:
    /// - Basic/Overtime/earning EmployeeSalary lines: + SalariesExpense, + SalariesPayable.
    /// - Deduction-type EmployeeSalary lines and Absence: − both (less expense recognized, less owed —
    ///   there is no dedicated role for a generic recurring deduction, so it nets against the wage it
    ///   reduces rather than crediting an unspecified account).
    /// - EmployerSocialInsurance: + SocialInsuranceExpense, + SocialInsurancePayable (the employer's
    ///   own cost, on top of Gross — never touches SalariesPayable).
    /// - LegalSocialInsurance/LegalTax/LegalMartyrsFund (the employee's withheld shares): − SalariesPayable,
    ///   + the matching *Payable role.
    /// Tips are excluded here — handled as the flat TipsPayable→SalariesPayable reclassification lines
    /// in the template itself, not per-employee (rule 39 doesn't tie a tip payout to a cost center).
    /// Amounts that net to zero or negative are dropped per role/employee (§35's exception review, not
    /// this posting step, is where a genuinely negative net should have already been caught).
    /// </summary>
    private async Task<Dictionary<string, IReadOnlyList<PostingGroupItem>>> BuildAccrualGroupsAsync(
        IReadOnlyList<PayrollLine> lines, IReadOnlyDictionary<long, Employee> employees, CancellationToken cancellationToken)
    {
        var roleAccounts = await PayrollPostingRules.ResolveRoleAccountsAsync(db, current.CompanyId, AccrualRoles, cancellationToken);

        var byRoleAndEmployee = new Dictionary<CompanyAccountRole, Dictionary<long, decimal>>();
        void Add(CompanyAccountRole role, long employeeId, decimal amount)
        {
            var byEmployee = byRoleAndEmployee.TryGetValue(role, out var existing) ? existing : byRoleAndEmployee[role] = [];
            byEmployee[employeeId] = byEmployee.GetValueOrDefault(employeeId) + amount;
        }

        foreach (var line in lines)
        {
            switch (line.SourceType)
            {
                case PayrollLineSource.Basic:
                case PayrollLineSource.Overtime:
                    Add(CompanyAccountRole.SalariesExpense, line.EmployeeId, line.Amount);
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, line.Amount);
                    break;
                case PayrollLineSource.EmployeeSalary:
                    var signed = line.SalaryComponent!.ComponentType == SalaryComponentType.Earning ? line.Amount : -line.Amount;
                    Add(CompanyAccountRole.SalariesExpense, line.EmployeeId, signed);
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, signed);
                    break;
                case PayrollLineSource.Absence:
                    Add(CompanyAccountRole.SalariesExpense, line.EmployeeId, -line.Amount);
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, -line.Amount);
                    break;
                case PayrollLineSource.EmployerSocialInsurance:
                    Add(CompanyAccountRole.SocialInsuranceExpense, line.EmployeeId, line.Amount);
                    Add(CompanyAccountRole.SocialInsurancePayable, line.EmployeeId, line.Amount);
                    break;
                case PayrollLineSource.LegalSocialInsurance:
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, -line.Amount);
                    Add(CompanyAccountRole.SocialInsurancePayable, line.EmployeeId, line.Amount);
                    break;
                case PayrollLineSource.LegalTax:
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, -line.Amount);
                    Add(CompanyAccountRole.PayrollTaxPayable, line.EmployeeId, line.Amount);
                    break;
                case PayrollLineSource.LegalMartyrsFund:
                    Add(CompanyAccountRole.SalariesPayable, line.EmployeeId, -line.Amount);
                    Add(CompanyAccountRole.MartyrsFundPayable, line.EmployeeId, line.Amount);
                    break;
                // Tips: handled outside this loop. Penalty/AdvanceInstallment/PriorPeriodAdjustment:
                // no lines exist yet (Phase 5 / not wired — Phase-4-Research.md §2.5, class doc of
                // PayrollCalculationService).
            }
        }

        var costCenterCache = new Dictionary<long, IReadOnlyDictionary<long, long>>();
        async Task<IReadOnlyDictionary<long, long>> CostCenterFor(long employeeId)
        {
            if (costCenterCache.TryGetValue(employeeId, out var cached))
            {
                return cached;
            }
            var dims = await PayrollPostingRules.CostCenterAsync(db, employees[employeeId], cancellationToken);
            costCenterCache[employeeId] = dims;
            return dims;
        }

        var groups = new Dictionary<string, IReadOnlyList<PostingGroupItem>>();
        foreach (var (role, byEmployee) in byRoleAndEmployee)
        {
            if (!roleAccounts.TryGetValue(role, out var accountId))
            {
                throw new BusinessRuleException(
                    "PAY-ACCOUNT-NOT-MAPPED", $"حساب الترحيل الافتراضي للدور {role} لسه ملهوش حساب مربوط — راجع شاشة حسابات الترحيل الافتراضية.");
            }

            var items = new List<PostingGroupItem>();
            foreach (var (employeeId, amount) in byEmployee)
            {
                if (amount <= 0)
                {
                    continue;
                }
                items.Add(new PostingGroupItem(accountId, amount, await CostCenterFor(employeeId)));
            }
            groups[GroupNameFor(role)] = items;
        }

        return groups;
    }

    private static string GroupNameFor(CompanyAccountRole role) => role switch
    {
        CompanyAccountRole.SalariesExpense => PostingScreenCatalog.SalariesExpenseGroup,
        CompanyAccountRole.SocialInsuranceExpense => PostingScreenCatalog.SocialInsuranceExpenseGroup,
        CompanyAccountRole.SalariesPayable => PostingScreenCatalog.SalariesPayableGroup,
        CompanyAccountRole.SocialInsurancePayable => PostingScreenCatalog.SocialInsurancePayableGroup,
        CompanyAccountRole.PayrollTaxPayable => PostingScreenCatalog.PayrollTaxPayableGroup,
        CompanyAccountRole.MartyrsFundPayable => PostingScreenCatalog.MartyrsFundPayableGroup,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}

/// <summary>Posted → Paid (rule 40: cash payment voucher in v1 — CompanyAccountRole.Cash
/// unconditionally, bank transfer file is explicitly a later phase in the module doc itself). Closes
/// the PayrollPeriod for a Regular run, matching the module doc's state diagram
/// ("→ Paid → [PayrollPeriod: Closed]").</summary>
public sealed record PayPayrollRunCommand(long PayrollRunId) : IRequest;

public sealed class PayPayrollRunCommandValidator : AbstractValidator<PayPayrollRunCommand>
{
    public PayPayrollRunCommandValidator() => RuleFor(x => x.PayrollRunId).GreaterThan(0);
}

public sealed class PayPayrollRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IPostingTemplateEngine posting)
    : IRequestHandler<PayPayrollRunCommand>
{
    public async Task Handle(PayPayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        if (run.Status != PayrollRunStatus.Posted)
        {
            throw new BusinessRuleException("PAY-RUN-NOT-POSTED", "لازم يكون التشغيل مترحّل قبل الصرف.");
        }

        var result = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = current.CompanyId,
            ScreenCode = PostingScreenCatalog.PayrollRun,
            SourceModule = SourceModule.Payroll,
            SourceDocumentType = SourceDocumentType.Payroll,
            SourceDocumentId = run.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = $"صرف رواتب — تشغيل رقم {run.Id}",
            IdempotencyKey = PostingKeys.For(current.CompanyId, "PayrollRun.Pay", run.Id),
            Context = PostingContext.Create(fields: new Dictionary<string, object?> { ["Stage"] = "Payment", ["TotalNet"] = run.TotalNet })
        }, cancellationToken);

        run.PaymentJournalEntryId = result?.Id;
        run.Status = PayrollRunStatus.Paid;

        if (run.RunType == PayrollRunType.Regular)
        {
            var period = await db.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
            if (period is not null)
            {
                period.Status = PayrollPeriodStatus.Closed;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Posted or Paid → Reversed (§4.3: "قيد عكسي بصلاحية عالية، ويتاح بعده تشغيل جديد بنفس المفتاح"
/// — Reversed is excluded from the IdempotencyKey's unique filter, Sub-Batch 4.4). Reverses both
/// journal entries when the run had reached Paid, reopens the PayrollPeriod for a Regular run (undoing
/// PayPayrollRunCommand's Close), and removes the Payslips — they were never valid to begin with once
/// the run itself is undone, and rule 38 only ever showed them because the run was Posted.</summary>
public sealed record ReversePayrollRunCommand(long PayrollRunId, string Reason) : IRequest;

public sealed class ReversePayrollRunCommandValidator : AbstractValidator<ReversePayrollRunCommand>
{
    public ReversePayrollRunCommandValidator()
    {
        RuleFor(x => x.PayrollRunId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReversePayrollRunCommandHandler(IApplicationDbContext db, IPostingTemplateEngine posting)
    : IRequestHandler<ReversePayrollRunCommand>
{
    public async Task Handle(ReversePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        if (run.Status is not (PayrollRunStatus.Posted or PayrollRunStatus.Paid))
        {
            throw new BusinessRuleException("PAY-RUN-NOT-REVERSIBLE", "التشغيل لازم يكون مترحّل أو مصروف قبل الإلغاء.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (run.PaymentJournalEntryId is { } paymentEntryId)
        {
            await posting.ReverseAsync(paymentEntryId, today, $"إلغاء صرف رواتب {run.RunNumber}: {request.Reason}", cancellationToken);
        }
        if (run.JournalEntryId is { } accrualEntryId)
        {
            await posting.ReverseAsync(accrualEntryId, today, $"إلغاء استحقاق رواتب {run.RunNumber}: {request.Reason}", cancellationToken);
        }

        var payslips = await db.Payslips.Where(p => p.PayrollRunId == run.Id).ToListAsync(cancellationToken);
        db.Payslips.RemoveRange(payslips);

        if (run.RunType == PayrollRunType.Regular)
        {
            var period = await db.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == run.PayrollPeriodId, cancellationToken);
            if (period is not null)
            {
                period.Status = PayrollPeriodStatus.Open;
            }
        }

        run.Status = PayrollRunStatus.Reversed;
        await db.SaveChangesAsync(cancellationToken);
    }
}
