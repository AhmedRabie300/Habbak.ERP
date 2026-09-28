using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Approvals;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12.2 — a named approval chain. Versioned exactly like
/// PostingTemplate (Docs/Modules/00-Posting-Engine-Architecture.md §3.1): editing a workflow that
/// has already started at least one ApprovalInstance creates a new version rather than mutating the
/// old one, so a historical instance stays explainable by the rules that actually built it.
/// Versions of one workflow share a FamilyId.
/// </summary>
public class ApprovalWorkflow : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    /// <summary>The same for every version of one workflow; a brand-new workflow gets a new one.</summary>
    public Guid FamilyId { get; set; } = Guid.NewGuid();

    public int VersionNumber { get; set; } = 1;
    public long? PreviousVersionId { get; set; }
    public bool IsCurrentVersion { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public ICollection<ApprovalWorkflowStep> Steps { get; set; } = new List<ApprovalWorkflowStep>();
}

/// <summary>
/// One ordered step of a workflow. Ordering between steps is StepOrder alone — §12.3's deliberate
/// removal of a "Sequential" mode means a step never encodes ordering itself, only how its own
/// approvers combine (<see cref="Mode"/>).
/// </summary>
public class ApprovalWorkflowStep : AuditableEntity
{
    public long ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }

    public int StepOrder { get; set; }
    public ApprovalStepMode Mode { get; set; } = ApprovalStepMode.AnyOne;

    public ICollection<ApprovalStepApprover> Approvers { get; set; } = new List<ApprovalStepApprover>();
}

/// <summary>
/// One eligible approver of a step. A step can carry several of these, of different
/// <see cref="ApprovalApproverType"/> kinds at once (§12.2 — this is why it is its own entity and
/// not a column squeezed onto the step).
/// </summary>
public class ApprovalStepApprover : AuditableEntity
{
    public long ApprovalWorkflowStepId { get; set; }
    public ApprovalWorkflowStep? ApprovalWorkflowStep { get; set; }

    public ApprovalApproverType ApproverType { get; set; }

    /// <summary>
    /// EmployeeId for <see cref="ApprovalApproverType.SpecificEmployee"/>, RoleId for
    /// <see cref="ApprovalApproverType.Role"/>, JobGradeId for <see cref="ApprovalApproverType.JobGrade"/>.
    /// Null for <see cref="ApprovalApproverType.DirectManager"/> — resolved at approval time from the
    /// requester's own Employee.ManagerId (§12.4 rule 8), never stored on the workflow.
    /// </summary>
    public long? ApproverReferenceId { get; set; }
}

/// <summary>
/// Links a <see cref="Common.Screen"/> to the workflow that governs it. A screen with no active
/// assignment keeps the plain, direct approval it has today (§12.4 rule 4).
/// </summary>
public class ApprovalWorkflowAssignment : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public long ScreenId { get; set; }
    public Screen? Screen { get; set; }

    public long ApprovalWorkflowId { get; set; }
    public ApprovalWorkflow? ApprovalWorkflow { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional activation threshold (§12.2's "شرط تفعيل اختياري"). Null means the workflow applies
    /// to every document on this screen once assigned; a value means it only applies once the
    /// triggering document's Amount is at or above it (mirrors <see cref="Common.Interfaces"/>'s
    /// pre-existing ApprovalWorkflowTrigger.Amount field, built for exactly this).
    /// </summary>
    public decimal? MinAmount { get; set; }
}

/// <summary>§12.3 — DirectManager/JobGrade are new; SpecificEmployee/Role existed in the original design.</summary>
public enum ApprovalApproverType
{
    SpecificEmployee = 1,
    Role = 2,

    /// <summary>Resolved from Employee.ManagerId at approval time, not workflow-design time (§12.4 rule 8).</summary>
    DirectManager = 3,

    /// <summary>Every active employee on this JobGrade is an eligible approver (§12.4 rule 9).</summary>
    JobGrade = 4
}

/// <summary>
/// §12.3 — deliberately just these two. "Sequential" is modeled by StepOrder across steps, not a
/// third mode here; "RequireMajority" is left out of the first version's scope entirely.
/// </summary>
public enum ApprovalStepMode
{
    AnyOne = 1,
    All = 2
}
