using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Approvals;

/// <summary>
/// Tracks one document's actual run through a workflow. EntityType/EntityId is a polymorphic
/// pointer at the document (mirrors WasteRecord.SourceDocumentType's own free-form-tag pattern) —
/// there is no single base document type all approvable entities share. WorkflowVersionNumber is a
/// snapshot of the version this instance actually ran under (§12.2), consistent with
/// ApprovalWorkflow's own versioning: a workflow edited after an instance started stays irrelevant
/// to that instance.
/// </summary>
public class ApprovalInstance : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public string EntityType { get; set; } = null!;
    public long EntityId { get; set; }

    public long ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }
    public int WorkflowVersionNumber { get; set; }

    public long RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public decimal Amount { get; set; }

    public int CurrentStepOrder { get; set; } = 1;
    public ApprovalInstanceStatus Status { get; set; } = ApprovalInstanceStatus.Pending;

    /// <summary>
    /// Manual Fallback (§12.4 rule 10). While set, the current step's only eligible approver is this
    /// user, overriding whatever <see cref="ApprovalStepApprover"/> resolution would otherwise
    /// produce — the escape hatch for "the original approver is on leave." Cleared automatically
    /// whenever <see cref="CurrentStepOrder"/> advances, since a reassignment is for one step only.
    /// </summary>
    public long? ManualReassignedToUserId { get; set; }

    public ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();
}

/// <summary>One recorded action against an instance — an approval, a rejection, or a manual reassignment.</summary>
public class ApprovalAction : AuditableEntity
{
    public long ApprovalInstanceId { get; set; }
    public ApprovalInstance? ApprovalInstance { get; set; }

    public int StepOrder { get; set; }
    public long ActionByUserId { get; set; }
    public ApprovalActionType ActionType { get; set; }

    /// <summary>Mandatory for <see cref="ApprovalActionType.Reject"/> (§12.4 rule 2's rejection needs a reason to be useful).</summary>
    public string? Reason { get; set; }

    /// <summary>Only for <see cref="ApprovalActionType.Reassigned"/> — the user the pending step moved to.</summary>
    public long? ReassignedToUserId { get; set; }

    public DateTime ActionAtUtc { get; set; }
}

public enum ApprovalInstanceStatus
{
    Pending = 1,
    Approved = 2,

    /// <summary>Terminal — §12.4 rule 2: a rejected instance is never resumed, a new document starts a new instance.</summary>
    Rejected = 3
}

public enum ApprovalActionType
{
    Approve = 1,
    Reject = 2,
    Reassigned = 3
}
