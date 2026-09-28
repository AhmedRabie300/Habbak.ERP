import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../../ui-kit/Button';
import { Icon } from '../../ui-kit/Icon';
import { Card, CardBody } from '../../ui-kit/Card';
import { SectionTabs } from '../../ui-kit/SectionTabs';
import { formatRelativeTime } from '../../lib/relativeTime';
import { useDeleteNotification, useMarkAllNotificationsAsRead, useMarkNotificationAsRead, useMyNotifications } from './api';
import type { Notification, NotificationFilter } from './types';

function actionLinkFor(notification: Notification): string | null {
  if (notification.relatedEntityType === 'ApprovalInstance' && notification.requiresAction) {
    return '/approvals/pending';
  }
  return null;
}

/** /notifications — Docs/Implementation/HR-MASTER-PLAN.md §Phase 2.5. No [Screen] gate: personal, per-user data (Phase-2.5-Research.md §1.7). */
export function NotificationsPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [filter, setFilter] = useState<NotificationFilter>('All');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useMyNotifications(filter, page);
  const markAsRead = useMarkNotificationAsRead();
  const markAllAsRead = useMarkAllNotificationsAsRead();
  const deleteNotification = useDeleteNotification();

  const onRowAction = (n: Notification) => {
    if (!n.isRead) markAsRead.mutate(n.id);
    const link = actionLinkFor(n);
    if (link) navigate(link);
  };

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('notifications.title')}</h2>
        <Button variant="secondary" onClick={() => markAllAsRead.mutate()}>{t('notifications.markAllRead')}</Button>
      </div>

      <SectionTabs
        items={[
          { id: 'All', label: t('notifications.filterAll') },
          { id: 'PendingAction', label: t('notifications.filterPendingAction') },
          { id: 'Unread', label: t('notifications.filterUnread') }
        ]}
        activeId={filter}
        onChange={(id) => { setFilter(id as NotificationFilter); setPage(1); }}
      />

      {isLoading && <div>{t('common.loading')}</div>}
      {!isLoading && (data?.items.length ?? 0) === 0 && <Card><CardBody>{t('notifications.empty')}</CardBody></Card>}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {data?.items.map((n) => (
          <Card key={n.id} style={{ background: n.isRead ? undefined : 'var(--color-surface-2)' }}>
            <CardBody style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4, minWidth: 0 }}>
                <strong style={{ fontSize: 14 }}>{i18n.language === 'ar' ? n.titleAr : n.titleEn}</strong>
                <span style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{i18n.language === 'ar' ? n.bodyAr : n.bodyEn}</span>
                <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>{formatRelativeTime(n.createdAtUtc, i18n.language)}</span>
              </div>
              <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                {actionLinkFor(n) && (
                  <Button variant="primary" onClick={() => onRowAction(n)}>{t('notifications.actionLink')}</Button>
                )}
                {!n.isRead && !actionLinkFor(n) && (
                  <Button variant="secondary" onClick={() => markAsRead.mutate(n.id)}>{t('notifications.markRead')}</Button>
                )}
                <Button variant="ghost" onClick={() => deleteNotification.mutate(n.id)}>
                  <Icon name="trash" size={14} />
                </Button>
              </div>
            </CardBody>
          </Card>
        ))}
      </div>

      {totalPages > 1 && (
        <div style={{ display: 'flex', justifyContent: 'center', gap: 8, alignItems: 'center' }}>
          <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>{t('common.previous')}</Button>
          <span style={{ fontSize: 13 }}>{page} / {totalPages}</span>
          <Button variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>{t('common.next')}</Button>
        </div>
      )}
    </div>
  );
}
