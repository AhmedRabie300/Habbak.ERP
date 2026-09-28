/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5 — minimal in-app notifications. */

export type NotificationType =
  | 'ApprovalPending'
  | 'ApprovalApproved'
  | 'ApprovalRejected'
  | 'ApprovalReassigned'
  | 'CustodySettlementRequired'
  | 'PayrollExceptionReview'
  | 'DocumentExpiringSoon'
  | 'EmployeeTerminated'
  | 'PayrollRunReadyForApproval'
  | 'GeneralInfo';

export type NotificationFilter = 'All' | 'PendingAction' | 'Unread';

export interface Notification {
  id: number;
  type: NotificationType;
  titleAr: string;
  titleEn: string;
  bodyAr: string;
  bodyEn: string;
  requiresAction: boolean;
  relatedEntityType: string | null;
  relatedEntityId: number | null;
  isRead: boolean;
  readAtUtc: string | null;
  createdAtUtc: string;
}
