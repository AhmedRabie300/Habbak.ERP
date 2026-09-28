using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Notifications;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Services;

/// <summary>
/// The real Approval Workflow Engine (00-Project-Overview.md §12), replacing
/// NullApprovalWorkflowService (Docs/Implementation/Phase-2-Research.md §1.1/§1.2 — registered in
/// Infrastructure/DependencyInjection.cs, this phase's Sub-Batch 2.6).
/// </summary>
public sealed class ApprovalWorkflowService(IApplicationDbContext db, IApprovalStepResolutionService resolution, INotificationService notifications) : IApprovalWorkflowService
{
    public async Task<long?> TryStartApprovalAsync(ApprovalWorkflowTrigger trigger, CancellationToken cancellationToken = default)
    {
        var screenCode = ApprovalTriggerScreenMap.ScreenCodeFor(trigger.EntityType);

        var screen = await db.Screens.FirstOrDefaultAsync(s => s.Code == screenCode, cancellationToken);
        if (screen is null)
        {
            return null;
        }

        var assignment = await db.ApprovalWorkflowAssignments
            .Include(a => a.ApprovalWorkflow)
            .FirstOrDefaultAsync(a => a.CompanyId == trigger.CompanyId && a.ScreenId == screen.Id && a.IsActive, cancellationToken);

        // Rule 4 — a screen with no active assignment keeps the plain, direct approval it has today.
        if (assignment?.ApprovalWorkflow is null || !assignment.ApprovalWorkflow.IsActive)
        {
            return null;
        }

        if (assignment.MinAmount is not null && trigger.Amount < assignment.MinAmount)
        {
            return null;
        }

        var firstStep = await db.ApprovalWorkflowSteps
            .Where(s => s.ApprovalWorkflowId == assignment.ApprovalWorkflowId)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstStep is null)
        {
            // A workflow with zero steps cannot gate anything — the create/update validator refuses
            // this shape, so a workflow reaching here with none means it predates that validation.
            return null;
        }

        var instance = new ApprovalInstance
        {
            CompanyId = trigger.CompanyId,
            EntityType = trigger.EntityType,
            EntityId = trigger.EntityId,
            ApprovalWorkflowId = assignment.ApprovalWorkflowId,
            WorkflowVersionNumber = assignment.ApprovalWorkflow.VersionNumber,
            RequestedByUserId = trigger.RequestedByUserId,
            RequestedAtUtc = DateTime.UtcNow,
            Amount = trigger.Amount,
            CurrentStepOrder = firstStep.StepOrder,
            Status = ApprovalInstanceStatus.Pending
        };

        db.ApprovalInstances.Add(instance);
        await db.SaveChangesAsync(cancellationToken);

        // Docs/Implementation/Phase-2.5-Research.md §1.6.1 — the first step's eligible approvers
        // need to know a request is waiting on them now, not whenever they next open "بانتظار
        // اعتمادي" on their own. A second save here (the instance's own Id is only known after the
        // first) — harmless, notifications don't need to be atomic with instance creation.
        var eligible = await resolution.GetEligibleApproverUserIdsAsync(instance, firstStep, cancellationToken);
        if (eligible.Count > 0)
        {
            await notifications.SendToMultipleAsync(
                eligible, NotificationType.ApprovalPending,
                titleAr: "طلب اعتماد جديد", titleEn: "New approval request",
                bodyAr: $"سلسلة \"{assignment.ApprovalWorkflow.NameAr}\" — طلب في انتظار اعتمادك.",
                bodyEn: $"Workflow \"{assignment.ApprovalWorkflow.NameEn}\" — a request is waiting on your approval.",
                requiresAction: true, relatedEntityType: "ApprovalInstance", relatedEntityId: instance.Id, cancellationToken: cancellationToken);
        }

        return instance.Id;
    }
}
