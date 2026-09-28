using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Notifications;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.4 — Draft→Calculated→PendingApproval
// (§4.3 of the module doc). Posted/Paid/Reversed belong to Sub-Batch 4.5 (Posting Integration) — not
// here, same layering as DepreciationCommands.cs (create/calculate here, post/reverse alongside the
// posting template).

public sealed record PayrollRunDto(
    long Id, long PayrollPeriodId, PayrollRunType RunType, PayrollRunStatus Status, int EmployeeCount,
    decimal TotalGross, decimal TotalDeductions, decimal TotalNet, decimal TotalEmployerCost);

internal static class PayrollRunKeys
{
    /// <summary>Rule 30 — same deterministic-Guid construction as DepreciationRunKeys.For
    /// (FixedAssets/DepreciationCommands.cs:28-40), extended with RunType/ReferenceId so Supplementary
    /// and FinalSettlement runs for the same month don't collide with each other or with Regular.</summary>
    public static Guid For(long companyId, int year, int month, PayrollRunType runType, long? referenceId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{companyId}:PayrollRun:{year:D4}-{month:D2}:{runType}:{referenceId}")).AsSpan(0, 16));

    public static PayrollRunDto Map(PayrollRun r) =>
        new(r.Id, r.PayrollPeriodId, r.RunType, r.Status, r.EmployeeCount, r.TotalGross, r.TotalDeductions, r.TotalNet, r.TotalEmployerCost);
}

public sealed record GetPayrollRunsQuery : IRequest<IReadOnlyList<PayrollRunDto>>;

public sealed class GetPayrollRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPayrollRunsQuery, IReadOnlyList<PayrollRunDto>>
{
    public async Task<IReadOnlyList<PayrollRunDto>> Handle(GetPayrollRunsQuery request, CancellationToken cancellationToken) =>
        (await db.PayrollRuns.AsNoTracking().OrderByDescending(r => r.Id).ToListAsync(cancellationToken)).Select(PayrollRunKeys.Map).ToList();
}

/// <summary>What creating a run answers: the run, and whether it already existed (rule 30) — same
/// shape/reasoning as DepreciationRunResult.</summary>
public sealed record PayrollRunResult(long Id, bool AlreadyExisted, PayrollRunStatus Status);

public sealed record CreatePayrollRunCommand(long PayrollPeriodId, PayrollRunType RunType, long? ReferenceId) : IRequest<PayrollRunResult>;

public sealed class CreatePayrollRunCommandValidator : AbstractValidator<CreatePayrollRunCommand>
{
    public CreatePayrollRunCommandValidator()
    {
        RuleFor(x => x.PayrollPeriodId).GreaterThan(0);
        RuleFor(x => x.ReferenceId).NotNull().When(x => x.RunType is PayrollRunType.Supplementary or PayrollRunType.FinalSettlement);
    }
}

public sealed class CreatePayrollRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<CreatePayrollRunCommand, PayrollRunResult>
{
    public async Task<PayrollRunResult> Handle(CreatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == request.PayrollPeriodId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Payroll.PayrollPeriod), request.PayrollPeriodId);

        var key = PayrollRunKeys.For(current.CompanyId, period.Year, period.Month, request.RunType, request.ReferenceId);
        if (await ExistingAsync(key, cancellationToken) is { } existing)
        {
            return existing;
        }

        var run = new PayrollRun
        {
            CompanyId = current.CompanyId,
            PayrollPeriodId = period.Id,
            RunType = request.RunType,
            IdempotencyKey = key,
            ReferenceId = request.ReferenceId,
            Status = PayrollRunStatus.Draft
        };

        db.PayrollRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new BusinessRuleException("PAY-RUN-EXISTS", "فيه تشغيل رواتب بنفس المفتاح اتعمل في نفس اللحظة — حدّث الشاشة.");
        }

        return new PayrollRunResult(run.Id, false, run.Status);
    }

    private async Task<PayrollRunResult?> ExistingAsync(Guid key, CancellationToken ct) =>
        await db.PayrollRuns.AsNoTracking()
            .Where(r => r.IdempotencyKey == key && r.Status != PayrollRunStatus.Reversed && r.Status != PayrollRunStatus.Rejected)
            .Select(r => new PayrollRunResult(r.Id, true, r.Status))
            .FirstOrDefaultAsync(ct);
}

/// <summary>Allowed while Draft or already Calculated (a pre-approval recompute, per the module doc's
/// state diagram "Calculated → Draft إعادة حساب قبل الاعتماد بس") — refused once the run has moved
/// past Calculated, since "مفيش إعادة حساب لتشغيل مرحّل".</summary>
public sealed record CalculatePayrollRunCommand(long PayrollRunId) : IRequest<PayrollCalculationResult>;

public sealed class CalculatePayrollRunCommandValidator : AbstractValidator<CalculatePayrollRunCommand>
{
    public CalculatePayrollRunCommandValidator() => RuleFor(x => x.PayrollRunId).GreaterThan(0);
}

public sealed class CalculatePayrollRunCommandHandler(IApplicationDbContext db, PayrollCalculationService calculation, INotificationService notifications)
    : IRequestHandler<CalculatePayrollRunCommand, PayrollCalculationResult>
{
    public async Task<PayrollCalculationResult> Handle(CalculatePayrollRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.Include(r => r.PayrollPeriod).FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        if (run.Status is not (PayrollRunStatus.Draft or PayrollRunStatus.Calculated))
        {
            throw new BusinessRuleException("PAY-RUN-NOT-CALCULABLE", "التشغيل ده اتجاوز مرحلة الحساب، مش ممكن يتحسب تاني.");
        }

        var result = await calculation.CalculateAsync(run, run.PayrollPeriod, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (result.Exceptions.Count > 0)
        {
            await NotifyExceptionsAsync(run, result.Exceptions.Count, cancellationToken);
        }

        return result;
    }

    private async Task NotifyExceptionsAsync(PayrollRun run, int exceptionCount, CancellationToken cancellationToken)
    {
        var recipientUserId = await db.HrSettingsRows.AsNoTracking()
            .Where(s => s.CompanyId == run.CompanyId).Select(s => s.CompanyDefaultApproverUserId).FirstOrDefaultAsync(cancellationToken);
        if (recipientUserId is null)
        {
            return; // No configured recipient — rule 35's review still applies, just not notified automatically (§2.7 gap: HR_SETTINGS UI to set this lands in Sub-Batch 4.6).
        }

        await notifications.SendAsync(
            recipientUserId.Value, NotificationType.PayrollExceptionReview,
            titleAr: "استثناءات رواتب تحتاج مراجعة", titleEn: "Payroll exceptions need review",
            bodyAr: $"تشغيل الرواتب رقم {run.Id} فيه {exceptionCount} استثناء يحتاج مراجعة قبل الاعتماد.",
            bodyEn: $"Payroll run #{run.Id} has {exceptionCount} exception(s) to review before approval.",
            requiresAction: true, relatedEntityType: "PayrollRun", relatedEntityId: run.Id, cancellationToken: cancellationToken);
    }
}

/// <summary>Calculated → PendingApproval, screen "PAY_PAYROLL_RUNS" (§4.8 row 11: HR ثم مالية) — same
/// EntityType-is-the-Screen.Code direct pass-through as HR_LEAVE_REQUESTS/HR_OVERTIME
/// (ApprovalTriggerScreenMap.cs's own comment already anticipates this, Phase-4-Research.md §1.8).</summary>
public sealed record SubmitPayrollRunForApprovalCommand(long PayrollRunId) : IRequest;

public sealed class SubmitPayrollRunForApprovalCommandValidator : AbstractValidator<SubmitPayrollRunForApprovalCommand>
{
    public SubmitPayrollRunForApprovalCommandValidator() => RuleFor(x => x.PayrollRunId).GreaterThan(0);
}

public sealed class SubmitPayrollRunForApprovalCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IApprovalWorkflowService approvalWorkflowService)
    : IRequestHandler<SubmitPayrollRunForApprovalCommand>
{
    public async Task Handle(SubmitPayrollRunForApprovalCommand request, CancellationToken cancellationToken)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == request.PayrollRunId, cancellationToken)
            ?? throw new NotFoundException(nameof(PayrollRun), request.PayrollRunId);

        if (run.Status != PayrollRunStatus.Calculated)
        {
            throw new BusinessRuleException("PAY-RUN-NOT-CALCULATED", "التشغيل لازم يكون محسوب الأول قبل ما يتبعت للاعتماد.");
        }

        var instanceId = await approvalWorkflowService.TryStartApprovalAsync(new ApprovalWorkflowTrigger
        {
            CompanyId = current.CompanyId,
            EntityType = "PAY_PAYROLL_RUNS",
            EntityId = run.Id,
            Amount = run.TotalNet,
            RequestedByUserId = current.UserId
        }, cancellationToken);

        if (instanceId is not null)
        {
            run.Status = PayrollRunStatus.PendingApproval;
            run.ApprovalInstanceId = instanceId;
        }
        else
        {
            run.Status = PayrollRunStatus.Approved; // Rule 4 — no workflow assigned to the screen yet means direct approval.
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
