interface BadgeProps {
  label: string;
  tone?: 'neutral' | 'success' | 'warning' | 'error' | 'info';
}

const toneColors: Record<NonNullable<BadgeProps['tone']>, { bg: string; fg: string }> = {
  neutral: { bg: '#eef0f3', fg: '#4b5563' },
  success: { bg: 'var(--color-success-bg)', fg: 'var(--color-success)' },
  warning: { bg: 'var(--color-warning-bg)', fg: 'var(--color-warning)' },
  error: { bg: 'var(--color-error-bg)', fg: 'var(--color-error)' },
  info: { bg: 'var(--color-info-bg)', fg: 'var(--color-info-fg)' }
};

/** Status badge used in every List screen's status column (00-Frontend-Specs.md, section 3.3) —
 * chip proportions from the system's "الشريط الموسّع" (Ribbon) design pass. */
export function Badge({ label, tone = 'neutral' }: BadgeProps) {
  const colors = toneColors[tone];
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        padding: '3px 10px',
        borderRadius: 'var(--radius-chip)',
        fontSize: 11,
        fontWeight: 700,
        background: colors.bg,
        color: colors.fg
      }}
    >
      {label}
    </span>
  );
}

const STATUS_TONE: Record<string, BadgeProps['tone']> = {
  Draft: 'neutral',
  Posted: 'success',
  Rejected: 'error',
  Reversed: 'warning',
  Cancelled: 'error',
  Active: 'success',
  UnderReview: 'warning',
  Inactive: 'neutral',
  PendingApproval: 'warning',
  PartiallyFulfilled: 'warning',
  Fulfilled: 'success',
  Pending: 'neutral',
  InProgress: 'warning',
  Completed: 'success',
  Approved: 'success',
  PendingSettlement: 'warning',
  Settled: 'info',
  Converted: 'info',
  Sent: 'warning',
  Confirmed: 'success',
  PartiallyReceived: 'warning',
  FullyReceived: 'success',
  Invoiced: 'info',
  PendingPayment: 'warning',
  PartiallyPaid: 'warning',
  Paid: 'success',
  Overdue: 'error',
  Archived: 'neutral',
  Awarded: 'success',
  Accepted: 'success',
  Expired: 'error',
  PartiallyDelivered: 'warning',
  Delivered: 'success',
  Responded: 'info',
  Declined: 'error',
  OnLeave: 'warning',
  Suspended: 'error',
  Terminated: 'error'
};

import { useTranslation } from 'react-i18next';

export function StatusBadge({ status }: { status: string }) {
  const { t } = useTranslation();
  return <Badge label={t(`status.${status}`, status)} tone={STATUS_TONE[status] ?? 'neutral'} />;
}
