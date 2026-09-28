using FluentValidation;
using Habbak.ERP.Application.Approvals.Outcomes;
using Habbak.ERP.Application.Approvals.Services;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Notifications;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Commands;

// ---------------------------------------------------------------------------- approve

public sealed record ApproveStepCommand(long InstanceId, string? Reason) : IRequest;

public sealed class ApproveStepCommandValidator : AbstractValidator<ApproveStepCommand>
{
    public ApproveStepCommandValidator() => RuleFor(x => x.InstanceId).GreaterThan(0);
}

public sealed class ApproveStepCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IApprovalStepResolutionService resolution,
    INotificationService notifications,
    IEnumerable<IApprovalOutcomeHandler> outcomeHandlers) : IRequestHandler<ApproveStepCommand>
{
    public async Task Handle(ApproveStepCommand request, CancellationToken cancellationToken)
    {
        var instance = await db.ApprovalInstances.Include(i => i.ApprovalWorkflow)
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalInstance), request.InstanceId);

        var actingUserId = currentCompanyContext.UserId;
        var step = await ApprovalActionGuards.ValidateActingUserAsync(db, resolution, instance, actingUserId, cancellationToken);

        db.ApprovalActions.Add(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepOrder = step.StepOrder,
            ActionByUserId = actingUserId,
            ActionType = ApprovalActionType.Approve,
            Reason = request.Reason,
            ActionAtUtc = DateTime.UtcNow
        });

        var eligible = await resolution.GetEligibleApproverUserIdsAsync(instance, step, cancellationToken);

        var stepSatisfied = step.Mode == ApprovalStepMode.AnyOne
            || await IsAllModeSatisfiedAsync(db, instance, step, eligible, actingUserId, cancellationToken);

        if (!stepSatisfied)
        {
            // Mode.All — still waiting on the rest of this step's eligible approvers.
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        instance.ManualReassignedToUserId = null; // §12.4 rule 10 — a reassignment covers one step only

        var nextStep = await db.ApprovalWorkflowSteps
            .Where(s => s.ApprovalWorkflowId == instance.ApprovalWorkflowId && s.StepOrder > step.StepOrder)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);

        var workflowNameAr = instance.ApprovalWorkflow?.NameAr ?? "";
        var workflowNameEn = instance.ApprovalWorkflow?.NameEn ?? "";

        if (nextStep is null)
        {
            instance.Status = ApprovalInstanceStatus.Approved;
            var handler = outcomeHandlers.FirstOrDefault(h => h.EntityType == instance.EntityType);
            if (handler is not null)
            {
                await handler.ApplyApprovedAsync(instance.EntityId, cancellationToken);
            }

            // Docs/Implementation/Phase-2.5-Research.md §1.6.2 — sent on final approval only, not
            // on every intermediate step of a multi-step chain (that would just be notification spam).
            await notifications.SendAsync(
                instance.RequestedByUserId, NotificationType.ApprovalApproved,
                titleAr: "تم اعتماد طلبك", titleEn: "Your request was approved",
                bodyAr: $"سلسلة \"{workflowNameAr}\" — تم اعتماد طلبك بالكامل.",
                bodyEn: $"Workflow \"{workflowNameEn}\" — your request was fully approved.",
                requiresAction: false, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);
        }
        else
        {
            instance.CurrentStepOrder = nextStep.StepOrder;

            var nextEligible = await resolution.GetEligibleApproverUserIdsAsync(instance, nextStep, cancellationToken);
            if (nextEligible.Count > 0)
            {
                await notifications.SendToMultipleAsync(
                    nextEligible, NotificationType.ApprovalPending,
                    titleAr: "طلب اعتماد جديد", titleEn: "New approval request",
                    bodyAr: $"سلسلة \"{workflowNameAr}\" — طلب في انتظار اعتمادك.",
                    bodyEn: $"Workflow \"{workflowNameEn}\" — a request is waiting on your approval.",
                    requiresAction: true, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> IsAllModeSatisfiedAsync(
        IApplicationDbContext db, ApprovalInstance instance, ApprovalWorkflowStep step, IReadOnlyList<long> eligible, long justApprovedByUserId, CancellationToken cancellationToken)
    {
        var alreadyApproved = await db.ApprovalActions
            .Where(a => a.ApprovalInstanceId == instance.Id && a.StepOrder == step.StepOrder && a.ActionType == ApprovalActionType.Approve)
            .Select(a => a.ActionByUserId)
            .ToListAsync(cancellationToken);

        var approvedSoFar = new HashSet<long>(alreadyApproved) { justApprovedByUserId };
        return eligible.All(approvedSoFar.Contains);
    }
}

// ---------------------------------------------------------------------------- reject

public sealed record RejectStepCommand(long InstanceId, string Reason) : IRequest;

public sealed class RejectStepCommandValidator : AbstractValidator<RejectStepCommand>
{
    public RejectStepCommandValidator()
    {
        RuleFor(x => x.InstanceId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("سبب الرفض إلزامي.");
    }
}

public sealed class RejectStepCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IApprovalStepResolutionService resolution,
    INotificationService notifications,
    IEnumerable<IApprovalOutcomeHandler> outcomeHandlers) : IRequestHandler<RejectStepCommand>
{
    public async Task Handle(RejectStepCommand request, CancellationToken cancellationToken)
    {
        var instance = await db.ApprovalInstances.Include(i => i.ApprovalWorkflow)
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalInstance), request.InstanceId);

        var actingUserId = currentCompanyContext.UserId;
        var step = await ApprovalActionGuards.ValidateActingUserAsync(db, resolution, instance, actingUserId, cancellationToken);

        db.ApprovalActions.Add(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepOrder = step.StepOrder,
            ActionByUserId = actingUserId,
            ActionType = ApprovalActionType.Reject,
            Reason = request.Reason,
            ActionAtUtc = DateTime.UtcNow
        });

        // §12.4 rule 2 — a single rejection at any step is final, regardless of the step's Mode
        // (AnyOne/All governs approval consensus only; rejecting never needs "everyone" to agree).
        instance.Status = ApprovalInstanceStatus.Rejected;

        var handler = outcomeHandlers.FirstOrDefault(h => h.EntityType == instance.EntityType);
        if (handler is not null)
        {
            await handler.ApplyRejectedAsync(instance.EntityId, cancellationToken);
        }

        await notifications.SendAsync(
            instance.RequestedByUserId, NotificationType.ApprovalRejected,
            titleAr: "تم رفض طلبك", titleEn: "Your request was rejected",
            bodyAr: $"سلسلة \"{instance.ApprovalWorkflow?.NameAr}\" — تم رفض طلبك. السبب: {request.Reason}",
            bodyEn: $"Workflow \"{instance.ApprovalWorkflow?.NameEn}\" — your request was rejected. Reason: {request.Reason}",
            requiresAction: false, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------- manual reassign (fallback)

/// <summary>§12.4 rule 10 — the Manual Fallback, gated by the SETTINGS_APPROVAL_WORKFLOWS/ManualReassign button permission at the API layer.</summary>
public sealed record ReassignInstanceCommand(long InstanceId, long NewApproverUserId, string? Reason) : IRequest;

public sealed class ReassignInstanceCommandValidator : AbstractValidator<ReassignInstanceCommand>
{
    public ReassignInstanceCommandValidator()
    {
        RuleFor(x => x.InstanceId).GreaterThan(0);
        RuleFor(x => x.NewApproverUserId).GreaterThan(0);
    }
}

public sealed class ReassignInstanceCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IApprovalStepResolutionService resolution, INotificationService notifications)
    : IRequestHandler<ReassignInstanceCommand>
{
    public async Task Handle(ReassignInstanceCommand request, CancellationToken cancellationToken)
    {
        var instance = await db.ApprovalInstances.Include(i => i.ApprovalWorkflow)
            .FirstOrDefaultAsync(i => i.Id == request.InstanceId, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalInstance), request.InstanceId);

        if (instance.Status != ApprovalInstanceStatus.Pending)
        {
            throw new BusinessRuleException("APPROVAL-INSTANCE-NOT-PENDING", "الطلب ده مش في حالة انتظار اعتماد.");
        }

        var currentStep = await db.ApprovalWorkflowSteps.FirstOrDefaultAsync(
            s => s.ApprovalWorkflowId == instance.ApprovalWorkflowId && s.StepOrder == instance.CurrentStepOrder, cancellationToken)
            ?? throw new BusinessRuleException("APPROVAL-STEP-NOT-FOUND", "خطوة الاعتماد الحالية غير موجودة.");

        // Resolved before the reassignment takes effect, so this is really "who was eligible a
        // moment ago" — the set ManualReassignedToUserId is about to replace.
        var previousEligible = await resolution.GetEligibleApproverUserIdsAsync(instance, currentStep, cancellationToken);

        instance.ManualReassignedToUserId = request.NewApproverUserId;

        db.ApprovalActions.Add(new ApprovalAction
        {
            ApprovalInstanceId = instance.Id,
            StepOrder = instance.CurrentStepOrder,
            ActionByUserId = currentCompanyContext.UserId,
            ActionType = ApprovalActionType.Reassigned,
            ReassignedToUserId = request.NewApproverUserId,
            Reason = request.Reason,
            ActionAtUtc = DateTime.UtcNow
        });

        var workflowNameAr = instance.ApprovalWorkflow?.NameAr ?? "";
        var workflowNameEn = instance.ApprovalWorkflow?.NameEn ?? "";

        var oldApprovers = previousEligible.Where(u => u != request.NewApproverUserId).ToList();
        if (oldApprovers.Count > 0)
        {
            await notifications.SendToMultipleAsync(
                oldApprovers, NotificationType.ApprovalReassigned,
                titleAr: "تم إعادة تسكين طلب", titleEn: "A request was reassigned",
                bodyAr: $"سلسلة \"{workflowNameAr}\" — طلب كان مستني اعتمادك اتنقل لمعتمد تاني.",
                bodyEn: $"Workflow \"{workflowNameEn}\" — a request waiting on your approval was reassigned.",
                requiresAction: false, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);
        }

        await notifications.SendAsync(
            request.NewApproverUserId, NotificationType.ApprovalPending,
            titleAr: "طلب اعتماد جديد", titleEn: "New approval request",
            bodyAr: $"سلسلة \"{workflowNameAr}\" — طلب في انتظار اعتمادك (إعادة تسكين يدوي).",
            bodyEn: $"Workflow \"{workflowNameEn}\" — a request is waiting on your approval (manually reassigned).",
            requiresAction: true, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal static class ApprovalActionGuards
{
    /// <summary>Instance must still be Pending, the actor must not be the requester (rule 7), and must be one of the current step's resolved eligible approvers.</summary>
    public static async Task<ApprovalWorkflowStep> ValidateActingUserAsync(
        IApplicationDbContext db, IApprovalStepResolutionService resolution, ApprovalInstance instance, long actingUserId, CancellationToken cancellationToken)
    {
        if (instance.Status != ApprovalInstanceStatus.Pending)
        {
            throw new BusinessRuleException("APPROVAL-INSTANCE-NOT-PENDING", "الطلب ده مش في حالة انتظار اعتماد.");
        }

        // §12.4 rule 7 — Self-Approval Guard, unconditional, even for a role/job-grade approver who
        // happens to be the requester.
        if (actingUserId == instance.RequestedByUserId)
        {
            throw new ForbiddenException("APPROVAL-SELF-APPROVAL-FORBIDDEN", "لا يمكنك اعتماد أو رفض طلبك الخاص.");
        }

        var step = await db.ApprovalWorkflowSteps.FirstOrDefaultAsync(
            s => s.ApprovalWorkflowId == instance.ApprovalWorkflowId && s.StepOrder == instance.CurrentStepOrder, cancellationToken)
            ?? throw new BusinessRuleException("APPROVAL-STEP-NOT-FOUND", "خطوة الاعتماد الحالية غير موجودة.");

        var eligible = await resolution.GetEligibleApproverUserIdsAsync(instance, step, cancellationToken);

        // §12.4 rule 9 — an empty result is a configuration gap (empty JobGrade, unresolved
        // DirectManager with no CompanyDefaultApproverUserId), surfaced explicitly, never a silent skip.
        if (eligible.Count == 0)
        {
            throw new BusinessRuleException(
                "APPROVAL-STEP-NO-ELIGIBLE-APPROVERS",
                "الخطوة الحالية مالهاش معتمد فعلي — راجع إعداد السلسلة (درجة وظيفية بلا موظفين، أو مدير مباشر بلا معتمد احتياطي مُعرَّف في إعدادات HR).");
        }

        if (!eligible.Contains(actingUserId))
        {
            throw new ForbiddenException("APPROVAL-NOT-ELIGIBLE-APPROVER", "أنت لست من معتمدي هذه الخطوة.");
        }

        return step;
    }
}
