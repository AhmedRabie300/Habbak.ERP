using Habbak.ERP.Application.Attendance.LeaveRequests;
using Habbak.ERP.Application.Common.Interfaces;

namespace Habbak.ERP.Application.Approvals.Outcomes;

/// <summary>Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — EntityType = Screen.Code مباشرة
/// ("HR_LEAVE_REQUESTS")، صفر Entry جديد في ApprovalTriggerScreenMap. نفس منطق ApplyApprovedAsync
/// المُستخدَم من SubmitLeaveRequestCommandHandler لما مفيش Workflow نشطة (LeaveRequestApplyHelpers).</summary>
public sealed class LeaveRequestApprovalOutcomeHandler(IApplicationDbContext db) : IApprovalOutcomeHandler
{
    public string EntityType => "HR_LEAVE_REQUESTS";

    public Task ApplyApprovedAsync(long entityId, CancellationToken cancellationToken) =>
        LeaveRequestApplyHelpers.ApplyApprovedAsync(db, entityId, cancellationToken);

    public Task ApplyRejectedAsync(long entityId, CancellationToken cancellationToken) =>
        LeaveRequestApplyHelpers.ApplyRejectedAsync(db, entityId, cancellationToken);
}
