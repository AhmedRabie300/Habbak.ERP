import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useButtonPermission } from '../../auth/access';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, getFieldErrorMessage } from '../../../app/api';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Button } from '../../../ui-kit/Button';
import { Badge } from '../../../ui-kit/Badge';
import { Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { useBranchesList } from '../../organization/branches/api';
import { todayLocal } from '../../../lib/date';

interface DayStatus {
  branchId: number;
  date: string;
  postingMode: 'PerTransaction' | 'PerShift' | 'PerDay';
  unpostedInvoiceCount: number;
  unpostedTotal: number;
  postedInvoiceCount: number;
  openShiftCount: number;
  postedEntryNumbers: string[];
}

interface CloseDayResult { postedInvoiceCount: number; postedTotal: number; entryNumber: string | null; postingConfigured: boolean }

/**
 * "إقفال اليوم" (decided 2026-09-18): posts one branch's still-unposted POS invoices for a date as a
 * single entry. Meant for branches in PerDay mode, but works in any mode — it only ever takes what
 * has no entry yet, which also makes it the way to post invoices left over from a mode switch.
 */
export function DayClosePanel() {
  const { t } = useTranslation();
  const canApprove = useButtonPermission('POS_SHIFTS', 'DayClose');
  const showToast = useToastStore((s) => s.show);
  const queryClient = useQueryClient();
  const { data: branches } = useBranchesList();
  const [branchId, setBranchId] = useState<number | ''>('');
  const [date, setDate] = useState(todayLocal());

  useEffect(() => {
    if (branches && branches.length > 0 && branchId === '') setBranchId(branches[0].id);
  }, [branches, branchId]);

  const { data: status } = useQuery({
    queryKey: ['pos-day-status', branchId, date],
    queryFn: async () => (await api.get<DayStatus>('/pos/day-close', { params: { branchId, date } })).data,
    enabled: branchId !== '' && !!date
  });

  const close = useMutation({
    mutationFn: async () => (await api.post<CloseDayResult>('/pos/day-close', { branchId, date })).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-day-status'] })
  });

  const onClose = async () => {
    if (!window.confirm(t('dayClose.confirm', { count: status?.unpostedInvoiceCount ?? 0, total: (status?.unpostedTotal ?? 0).toFixed(2) }))) return;
    try {
      const result = await close.mutateAsync();
      showToast(result.postingConfigured
        ? t('dayClose.done', { count: result.postedInvoiceCount, entry: result.entryNumber })
        : t('dayClose.notConfigured'), result.postingConfigured ? 'success' : 'error');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={{ fontWeight: 700, fontSize: 14, alignSelf: 'center', marginInlineEnd: 8 }}>{t('dayClose.title')}</div>
          <SearchableSelect
            value={branchId}
            onChange={(v) => setBranchId(v === '' ? '' : Number(v))}
            options={(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))}
            style={{ minWidth: 200 }}
          />
          <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} style={{ width: 170 }} />

          {status && (
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center', fontSize: 13 }}>
              <Badge label={t(`posSettings.postingModes.${status.postingMode}`)} tone="info" />
              {status.unpostedInvoiceCount > 0
                ? <Badge label={t('dayClose.unposted', { count: status.unpostedInvoiceCount, total: status.unpostedTotal.toFixed(2) })} tone="warning" />
                : <Badge label={t('dayClose.allPosted')} tone="success" />}
              {status.openShiftCount > 0 && <Badge label={t('dayClose.openShifts', { count: status.openShiftCount })} tone="neutral" />}
              {status.postedEntryNumbers.length > 0 && (
                <span style={{ color: 'var(--color-text-muted)' }}>{t('dayClose.entries')}: {status.postedEntryNumbers.join('، ')}</span>
              )}
            </div>
          )}

          <Button
            variant="primary"
            style={{ marginInlineStart: 'auto' }}
            disabled={!canApprove || !status || status.unpostedInvoiceCount === 0 || close.isPending}
            title={canApprove ? undefined : t('auth.noPermissionAction')}
            onClick={onClose}
          >
            {t('dayClose.button')}
          </Button>
        </div>
      </CardBody>
    </Card>
  );
}
