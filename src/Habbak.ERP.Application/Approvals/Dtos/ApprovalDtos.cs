using Habbak.ERP.Domain.Approvals;

namespace Habbak.ERP.Application.Approvals.Dtos;

public sealed record ApprovalStepApproverDto(long Id, ApprovalApproverType ApproverType, long? ApproverReferenceId, string? ApproverReferenceLabel);

public sealed record ApprovalWorkflowStepDto(long Id, int StepOrder, ApprovalStepMode Mode, IReadOnlyList<ApprovalStepApproverDto> Approvers);

public sealed record ApprovalWorkflowListItemDto(long Id, string Code, string NameAr, string NameEn, int VersionNumber, bool IsActive, int StepCount);

public sealed record ApprovalWorkflowDetailDto(long Id, string Code, string NameAr, string NameEn, int VersionNumber, bool IsActive, IReadOnlyList<ApprovalWorkflowStepDto> Steps);

public sealed record ScreenDto(long Id, string Code, string NameAr, string NameEn, string ModuleCode);

public sealed record ApprovalWorkflowAssignmentDto(long Id, long ScreenId, string ScreenCode, string ScreenNameAr, string ScreenNameEn, long ApprovalWorkflowId, string WorkflowNameAr, string WorkflowNameEn, bool IsActive, decimal? MinAmount);

public sealed record ApprovalActionDto(long Id, int StepOrder, long ActionByUserId, ApprovalActionType ActionType, string? Reason, long? ReassignedToUserId, DateTime ActionAtUtc);

public sealed record ApprovalInstanceDto(
    long Id, string EntityType, long EntityId, long ApprovalWorkflowId, string WorkflowNameAr, string WorkflowNameEn,
    int WorkflowVersionNumber, long RequestedByUserId, DateTime RequestedAtUtc, decimal Amount,
    int CurrentStepOrder, int TotalSteps, ApprovalInstanceStatus Status, IReadOnlyList<ApprovalActionDto> Actions);

/// <summary>One row of "بانتظار اعتمادي" — one instance the current user can act on right now.</summary>
public sealed record PendingApprovalDto(
    long InstanceId, string EntityType, long EntityId, string WorkflowNameAr, string WorkflowNameEn,
    long RequestedByUserId, DateTime RequestedAtUtc, decimal Amount, int CurrentStepOrder, int TotalSteps);
