/** Docs/Modules/00-Project-Overview.md §12 — Approval Workflow Engine (Phase 2). */

export type ApproverType = 'SpecificEmployee' | 'Role' | 'DirectManager' | 'JobGrade';
export type StepMode = 'AnyOne' | 'All';
export type InstanceStatus = 'Pending' | 'Approved' | 'Rejected';
export type ActionType = 'Approve' | 'Reject' | 'Reassigned';

export interface ApprovalStepApprover {
  id: number;
  approverType: ApproverType;
  approverReferenceId: number | null;
  approverReferenceLabel: string | null;
}

export interface ApprovalStepApproverInput {
  approverType: ApproverType;
  approverReferenceId: number | null;
}

export interface ApprovalWorkflowStep {
  id: number;
  stepOrder: number;
  mode: StepMode;
  approvers: ApprovalStepApprover[];
}

export interface ApprovalWorkflowStepInput {
  stepOrder: number;
  mode: StepMode;
  approvers: ApprovalStepApproverInput[];
}

export interface ApprovalWorkflowListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  versionNumber: number;
  isActive: boolean;
  stepCount: number;
}

export interface ApprovalWorkflowDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  versionNumber: number;
  isActive: boolean;
  steps: ApprovalWorkflowStep[];
}

export interface ApprovalWorkflowInput {
  code: string;
  nameAr: string;
  nameEn: string;
  steps: ApprovalWorkflowStepInput[];
}

export interface Screen {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  moduleCode: string;
}

export interface ApprovalWorkflowAssignment {
  id: number;
  screenId: number;
  screenCode: string;
  screenNameAr: string;
  screenNameEn: string;
  approvalWorkflowId: number;
  workflowNameAr: string;
  workflowNameEn: string;
  isActive: boolean;
  minAmount: number | null;
}

export interface ApprovalAction {
  id: number;
  stepOrder: number;
  actionByUserId: number;
  actionType: ActionType;
  reason: string | null;
  reassignedToUserId: number | null;
  actionAtUtc: string;
}

export interface ApprovalInstance {
  id: number;
  entityType: string;
  entityId: number;
  approvalWorkflowId: number;
  workflowNameAr: string;
  workflowNameEn: string;
  workflowVersionNumber: number;
  requestedByUserId: number;
  requestedAtUtc: string;
  amount: number;
  currentStepOrder: number;
  totalSteps: number;
  status: InstanceStatus;
  actions: ApprovalAction[];
}

export interface PendingApproval {
  instanceId: number;
  entityType: string;
  entityId: number;
  workflowNameAr: string;
  workflowNameEn: string;
  requestedByUserId: number;
  requestedAtUtc: string;
  amount: number;
  currentStepOrder: number;
  totalSteps: number;
}
