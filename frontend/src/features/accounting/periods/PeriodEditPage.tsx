import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { UserName } from '../../settings/security/UserName';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { getFieldErrorMessage } from '../../../app/api';
import { useClosePeriod, useCreatePeriod, usePeriod, useReopenPeriod } from './api';
import { toLocalDateString } from '../../../lib/date';

/** My Remarks/Remarks2.md, remark 2.3 — a whole calendar month starting today is a more useful
 * default here than "today" for both ends, since periodEnd always trails periodStart by design. */
function currentMonthBounds() {
  const now = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), 1);
  const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
  return { start: toLocalDateString(start), end: toLocalDateString(end) };
}

/** /accounting/accounting-periods/:id — My Remarks/Remarks2.md, remark 3.1: the standard Edit
 * screen for one financial period, replacing the old dual-panel single-page layout. Close is
 * gated by a `permissionKey` per remark 3.1's "صلاحية تحكم في إقفال الفترة" — inert until the
 * Permissions module lands (usePermission is still a fixed always-true stub, same as every other
 * permissionKey-carrying action would be), but the intent is recorded now rather than left unmarked. */
export function PeriodEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const periodId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: period, isLoading } = usePeriod(periodId);
  const { label } = useFieldLabels('ACCOUNTING_PERIODS');

  const [periodStart, setPeriodStart] = useState(() => currentMonthBounds().start);
  const [periodEnd, setPeriodEnd] = useState(() => currentMonthBounds().end);
  const createPeriod = useCreatePeriod();
  const closePeriod = useClosePeriod(periodId ?? 0);
  const reopenPeriod = useReopenPeriod(periodId ?? 0);

  const handleCreate = async () => {
    try {
      const newId = await createPeriod.mutateAsync({ periodStart, periodEnd });
      showToast(t('periods.createSuccess'), 'success');
      navigate(`/accounting/accounting-periods/${newId}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleClose = async () => {
    try {
      await closePeriod.mutateAsync();
      showToast(t('periods.closeSuccess'), 'success');
    } catch (error) {
      // Field-level errors (e.g. rule 8's checklist-incomplete message) aren't toasted centrally.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleReopen = async () => {
    try {
      await reopenPeriod.mutateAsync();
      showToast(t('periods.reopenSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('periods.newPeriod') : `${period?.periodStart ?? ''} → ${period?.periodEnd ?? ''}`}</h2>
        {period && <StatusBadge status={period.status} />}
      </div>

      <ActionBar
        primary={isNew ? { key: 'save', label: t('common.saveDraft'), onClick: handleCreate } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/accounting/accounting-periods') },
          ...(!isNew && period?.status === 'Open'
            ? [{ key: 'close', label: t('periods.closePeriod'), onClick: handleClose, disabled: !period.canClose, permissionKey: 'ACCOUNTING_PERIODS_CLOSE' }]
            : [])
        ]}
        destructive={
          !isNew && period?.status === 'Closed'
            ? [{ key: 'reopen', label: t('periods.reopenPeriod'), onClick: handleReopen, confirmMessage: t('periods.reopenPeriod'), permissionKey: 'ACCOUNTING_PERIODS_REOPEN' }]
            : []
        }
      />

      <Card>
        <CardBody>
          {isNew ? (
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('from', t('periods.from'))}>
                <Input type="date" value={periodStart} onChange={(e) => setPeriodStart(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={label('to', t('periods.to'))}>
                <Input type="date" value={periodEnd} onChange={(e) => setPeriodEnd(e.target.value)} />
              </FieldWrapper>
            </div>
          ) : (
            <>
              {period?.status === 'Open' && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {period.checklist.map((c) => (
                    <div key={c.itemKey} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
                      <span style={{ color: c.isSatisfied ? 'var(--color-success)' : 'var(--color-error)' }}>
                        {c.isSatisfied ? '✅' : '❌'}
                      </span>
                      <span style={{ flex: 1 }}>{c.label}</span>
                      <span style={{ color: 'var(--color-text-muted)' }}>{c.detail}</span>
                    </div>
                  ))}
                </div>
              )}

              {period?.status === 'Closed' && (
                <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>
                  {label('closedBy', t('periods.closedBy'))} <UserName id={period.closedByUserId} /> — {period.closedAtUtc}
                </p>
              )}
            </>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
