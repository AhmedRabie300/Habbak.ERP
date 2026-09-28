import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../ui-kit/Icon';
import { Button } from '../../ui-kit/Button';
import { formatRelativeTime } from '../../lib/relativeTime';
import { useMarkNotificationAsRead, usePendingActionCount, useRecentNotifications } from './api';
import type { Notification, NotificationType } from './types';

const TYPE_ICON: Record<NotificationType, 'bell' | 'check' | 'x' | 'reverse'> = {
  ApprovalPending: 'bell',
  ApprovalApproved: 'check',
  ApprovalRejected: 'x',
  ApprovalReassigned: 'reverse',
  CustodySettlementRequired: 'bell',
  PayrollExceptionReview: 'bell',
  DocumentExpiringSoon: 'bell',
  EmployeeTerminated: 'bell',
  PayrollRunReadyForApproval: 'bell',
  GeneralInfo: 'bell'
};

/** Where "action" on a notification actually goes — only ApprovalInstance/ApprovalPending has a
 * real destination today (the "بانتظار اعتمادي" worklist); other types are informational only. */
function actionLinkFor(notification: Notification): string | null {
  if (notification.relatedEntityType === 'ApprovalInstance' && notification.requiresAction) {
    return '/approvals/pending';
  }
  return null;
}

/** frontend/src/features/notifications/ — Docs/Implementation/Phase-2.5-Research.md: not in
 * ui-kit, since (unlike SectionTabs/DataGrid) it owns its own data fetching, not just props. */
export function NotificationBell() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const { data: pendingActionCount } = usePendingActionCount();
  const { data: recent, isLoading } = useRecentNotifications(open);
  const markAsRead = useMarkNotificationAsRead();

  useEffect(() => {
    if (!open) return;
    const onClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [open]);

  const onNotificationClick = (notification: Notification) => {
    if (!notification.isRead) markAsRead.mutate(notification.id);
    const link = actionLinkFor(notification);
    if (link) {
      setOpen(false);
      navigate(link);
    }
  };

  const count = pendingActionCount ?? 0;

  return (
    <div ref={containerRef} style={{ position: 'relative' }}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={t('notifications.title')}
        style={{
          position: 'relative', display: 'flex', alignItems: 'center', justifyContent: 'center',
          width: 36, height: 36, borderRadius: '50%', border: 'none', background: 'transparent',
          color: 'var(--color-text)', cursor: 'pointer'
        }}
      >
        <Icon name="bell" size={19} />
        {count > 0 && (
          <span
            style={{
              position: 'absolute', top: 2, insetInlineEnd: 2, minWidth: 16, height: 16, padding: '0 4px',
              borderRadius: 8, background: 'var(--color-error, #dc2626)', color: '#fff', fontSize: 10, fontWeight: 700,
              display: 'flex', alignItems: 'center', justifyContent: 'center', lineHeight: 1
            }}
          >
            {count > 99 ? '99+' : count}
          </span>
        )}
      </button>

      {open && (
        <div
          role="menu"
          style={{
            position: 'absolute', insetInlineEnd: 0, top: 42, width: 340, maxHeight: 420, overflowY: 'auto',
            background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)',
            border: '1px solid var(--color-border)', zIndex: 1000
          }}
        >
          <div style={{ padding: '10px 14px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 13 }}>
            {t('notifications.title')}
          </div>

          {isLoading && <div style={{ padding: 16, fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.loading')}</div>}
          {!isLoading && (recent?.items.length ?? 0) === 0 && (
            <div style={{ padding: 16, fontSize: 13, color: 'var(--color-text-muted)' }}>{t('notifications.empty')}</div>
          )}

          {recent?.items.map((n) => (
            <button
              key={n.id}
              type="button"
              onClick={() => onNotificationClick(n)}
              style={{
                display: 'flex', gap: 10, width: '100%', textAlign: 'start', padding: '10px 14px', border: 'none',
                borderBottom: '1px solid var(--color-border)', background: n.isRead ? 'transparent' : 'var(--color-surface-2)',
                cursor: 'pointer', fontFamily: 'inherit'
              }}
            >
              <Icon name={TYPE_ICON[n.type]} size={16} style={{ marginTop: 2, flexShrink: 0, color: 'var(--color-text-muted)' }} />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2, minWidth: 0 }}>
                <span style={{ fontSize: 13, fontWeight: n.isRead ? 500 : 700 }}>{i18n.language === 'ar' ? n.titleAr : n.titleEn}</span>
                <span style={{ fontSize: 12, color: 'var(--color-text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {i18n.language === 'ar' ? n.bodyAr : n.bodyEn}
                </span>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>{formatRelativeTime(n.createdAtUtc, i18n.language)}</span>
                {n.requiresAction && actionLinkFor(n) && (
                  <span style={{ fontSize: 11, fontWeight: 700, color: 'var(--color-gold-600, #b45309)' }}>{t('notifications.actionLink')} ←</span>
                )}
              </div>
            </button>
          ))}

          <div style={{ padding: 10 }}>
            <Button
              variant="secondary"
              style={{ width: '100%' }}
              onClick={() => {
                setOpen(false);
                navigate('/notifications');
              }}
            >
              {t('notifications.viewAll')}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
