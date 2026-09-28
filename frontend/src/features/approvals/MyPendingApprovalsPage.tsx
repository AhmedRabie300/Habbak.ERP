import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../ui-kit/Button';
import { Icon } from '../../ui-kit/Icon';
import { Card, CardBody } from '../../ui-kit/Card';
import { useToastStore } from '../../store/toastStore';
import { getFieldErrorMessage } from '../../app/api';
import { UserName } from '../settings/security/UserName';
import { useApproveInstance, useMyPendingApprovals, useRejectInstance } from '../settings/approvals/api';
import type { PendingApproval } from '../settings/approvals/types';

/** /approvals/pending — APPROVAL_MY_PENDING, the "بانتظار اعتمادي" worklist (00-Frontend-Specs.md §19). */
export function MyPendingApprovalsPage() {
  const { t } = useTranslation();
  const { data, isLoading } = useMyPendingApprovals();
  const approveMutation = useApproveInstance();
  const rejectMutation = useRejectInstance();
  const showToast = useToastStore((s) => s.show);
  const [rejecting, setRejecting] = useState<PendingApproval | null>(null);
  const [reason, setReason] = useState('');

  const onApprove = async (row: PendingApproval) => {
    try {
      await approveMutation.mutateAsync({ id: row.instanceId });
      showToast(t('approvals.pending.approved'), 'success');
    } catch (err) {
      const message = getFieldErrorMessage(err); if (message) showToast(message, 'error');
    }
  };

  const onConfirmReject = async () => {
    if (!rejecting || !reason.trim()) return;
    try {
      await rejectMutation.mutateAsync({ id: rejecting.instanceId, reason: reason.trim() });
      showToast(t('approvals.pending.rejected'), 'success');
      setRejecting(null);
      setReason('');
    } catch (err) {
      const message = getFieldErrorMessage(err); if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('approvals.pending.title')}</h2>

      {isLoading && <div>{t('common.loading')}</div>}
      {!isLoading && (data?.length ?? 0) === 0 && <Card><CardBody>{t('approvals.pending.empty')}</CardBody></Card>}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {data?.map((row) => (
          <Card key={row.instanceId}>
            <CardBody style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <strong>{row.workflowNameAr}</strong>
                <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
                  {row.entityType} #{row.entityId} — {t('approvals.pending.step')} {row.currentStepOrder}/{row.totalSteps}
                </span>
                <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
                  {t('approvals.pending.requestedBy')}: <UserName id={row.requestedByUserId} /> — {new Date(row.requestedAtUtc).toLocaleString()}
                </span>
                {row.amount > 0 && <span style={{ fontSize: 13, fontWeight: 600 }}>{row.amount.toLocaleString()}</span>}
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <Button variant="primary" onClick={() => onApprove(row)}>
                  <Icon name="check" size={14} />
                  {t('approvals.pending.approve')}
                </Button>
                <Button variant="secondary" onClick={() => { setRejecting(row); setReason(''); }}>
                  <Icon name="x" size={14} />
                  {t('approvals.pending.reject')}
                </Button>
              </div>
            </CardBody>
          </Card>
        ))}
      </div>

      {rejecting && (
        <div
          role="dialog"
          aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', backdropFilter: 'blur(2px)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setRejecting(null)}
        >
          <div
            style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', padding: 20, width: 420, display: 'flex', flexDirection: 'column', gap: 12 }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3 style={{ margin: 0 }}>{t('approvals.pending.rejectTitle')}</h3>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder={t('approvals.pending.rejectReasonPlaceholder')}
              rows={3}
              style={{ padding: 10, borderRadius: 'var(--radius-chip)', border: '1px solid var(--color-border)', fontFamily: 'inherit', fontSize: 14, resize: 'vertical' }}
            />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <Button variant="secondary" onClick={() => setRejecting(null)}>{t('common.cancel')}</Button>
              <Button variant="primary" disabled={!reason.trim()} onClick={onConfirmReject}>{t('approvals.pending.reject')}</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
