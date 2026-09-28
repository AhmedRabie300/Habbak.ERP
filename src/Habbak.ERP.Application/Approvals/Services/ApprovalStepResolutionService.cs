using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Services;

public sealed class ApprovalStepResolutionService(IApplicationDbContext db) : IApprovalStepResolutionService
{
    public async Task<IReadOnlyList<long>> GetEligibleApproverUserIdsAsync(
        ApprovalInstance instance, ApprovalWorkflowStep step, CancellationToken cancellationToken)
    {
        // Manual Fallback (§12.4 rule 10) overrides normal resolution entirely, but only for the
        // step it was actually reassigned at — a reassignment made at step 2 must not leak into
        // step 3 once the instance advances (ApproveStepCommand clears it on every advance).
        if (instance.ManualReassignedToUserId is not null && step.StepOrder == instance.CurrentStepOrder)
        {
            return [instance.ManualReassignedToUserId.Value];
        }

        var approvers = await db.ApprovalStepApprovers
            .Where(a => a.ApprovalWorkflowStepId == step.Id)
            .ToListAsync(cancellationToken);

        var eligible = new HashSet<long>();

        foreach (var approver in approvers)
        {
            switch (approver.ApproverType)
            {
                case ApprovalApproverType.SpecificEmployee:
                {
                    var userId = await db.Employees
                        .Where(e => e.Id == approver.ApproverReferenceId)
                        .Select(e => e.UserId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (userId is not null) eligible.Add(userId.Value);
                    break;
                }

                case ApprovalApproverType.Role:
                {
                    var userIds = await db.UserRoles
                        .Where(ur => ur.RoleId == approver.ApproverReferenceId)
                        .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                        .ToListAsync(cancellationToken);
                    foreach (var userId in userIds) eligible.Add(userId);
                    break;
                }

                case ApprovalApproverType.DirectManager:
                {
                    var managerUserId = await ResolveDirectManagerUserIdAsync(instance, cancellationToken);
                    if (managerUserId is not null) eligible.Add(managerUserId.Value);
                    break;
                }

                case ApprovalApproverType.JobGrade:
                {
                    var userIds = await db.Employees
                        .Where(e => e.JobGradeId == approver.ApproverReferenceId && e.IsActive && e.UserId != null)
                        .Select(e => e.UserId!.Value)
                        .ToListAsync(cancellationToken);
                    foreach (var userId in userIds) eligible.Add(userId);
                    break;
                }
            }
        }

        return eligible.ToList();
    }

    /// <summary>
    /// §12.4 rule 8: resolved from the requester's own Employee.ManagerId at approval time. Falls
    /// back to HrSettings.CompanyDefaultApproverUserId — not just when ManagerId is null, but also
    /// when the manager exists yet has no linked login account (Docs/Implementation/
    /// Phase-2-Research.md §3.4) — both are "no approver actually reachable," the case the rule means.
    /// </summary>
    private async Task<long?> ResolveDirectManagerUserIdAsync(ApprovalInstance instance, CancellationToken cancellationToken)
    {
        var requester = await db.Employees
            .Where(e => e.UserId == instance.RequestedByUserId && e.CompanyId == instance.CompanyId)
            .Select(e => new { e.ManagerId })
            .FirstOrDefaultAsync(cancellationToken);

        long? managerUserId = null;
        if (requester?.ManagerId is not null)
        {
            managerUserId = await db.Employees
                .Where(e => e.Id == requester.ManagerId)
                .Select(e => e.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (managerUserId is not null)
        {
            return managerUserId;
        }

        return await db.HrSettingsRows
            .Where(s => s.CompanyId == instance.CompanyId)
            .Select(s => s.CompanyDefaultApproverUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
