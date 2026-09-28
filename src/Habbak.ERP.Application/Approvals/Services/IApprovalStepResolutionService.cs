using Habbak.ERP.Domain.Approvals;

namespace Habbak.ERP.Application.Approvals.Services;

/// <summary>
/// Resolves who may act on one step of one running instance, right now — never at workflow design
/// time (00-Project-Overview.md §12.4 rule 8). Shared by ApproveStepCommand/RejectStepCommand (is
/// the acting user eligible?), GetMyPendingApprovalsQuery (which instances belong to the current
/// user's worklist?), and the workflow-start/step-advance path (is the next step even resolvable,
/// or is this a JobGrade/DirectManager configuration gap that needs to surface explicitly — rule 9)?
/// </summary>
public interface IApprovalStepResolutionService
{
    /// <summary>
    /// The distinct set of UserIds allowed to act on <paramref name="instance"/>'s current step
    /// right now. Empty means a configuration gap (an empty JobGrade, a DirectManager chain that
    /// resolves to nobody and no CompanyDefaultApproverUserId configured either) — callers surface
    /// this explicitly (rule 9), never treat it as "step satisfied" or silently skip it.
    /// </summary>
    Task<IReadOnlyList<long>> GetEligibleApproverUserIdsAsync(ApprovalInstance instance, ApprovalWorkflowStep step, CancellationToken cancellationToken);
}
