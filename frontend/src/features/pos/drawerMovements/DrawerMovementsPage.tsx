import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useButtonPermission } from '../../auth/access';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useShiftsList } from '../shifts/api';
import { useApproveDrawerMovement, useCreateDrawerMovement, useDrawerMovementsList, useRejectDrawerMovement } from './api';
import type { DrawerMovement, DrawerMovementType } from './types';
import type { PagedResult } from '../../../app/apiTypes';

const MOVEMENT_TYPES: DrawerMovementType[] = ['Drop', 'Pickup'];

/** /pos/drawer-movements — screen #16 (05-Module-POS-Shifts.md): إيداع/سحب بسير اعتماد (قاعدة
 * 6/32) — حركة Recorded لسه معلّقة ومالهاش أي تأثير على الفرق المتوقَّع لحد ما تتعتمد. */
export function DrawerMovementsPage() {
  const { t } = useTranslation();
  const canApprove = useButtonPermission('POS_DRAWER_MOVEMENTS', 'Approve');
  const canReject = useButtonPermission('POS_DRAWER_MOVEMENTS', 'Reject');
  const showToast = useToastStore((s) => s.show);

  const { data: terminals } = usePOSTerminalsList();
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const { data: shifts } = useShiftsList(posTerminalId === '' ? undefined : posTerminalId, undefined);
  const [shiftId, setShiftId] = useState<number | ''>('');

  useEffect(() => {
    if (terminals && terminals.length > 0 && posTerminalId === '') {
      setPosTerminalId(terminals[0].id);
    }
  }, [terminals, posTerminalId]);

  useEffect(() => {
    if (shifts && shifts.length > 0) {
      const openShift = shifts.find((s) => s.status === 'Open');
      setShiftId(openShift?.id ?? shifts[0].id);
    } else {
      setShiftId('');
    }
  }, [shifts]);

  const { data: movements, isLoading } = useDrawerMovementsList(shiftId === '' ? undefined : shiftId);
  const createMutation = useCreateDrawerMovement();
  const approveMutation = useApproveDrawerMovement();
  const rejectMutation = useRejectDrawerMovement();

  const [movementType, setMovementType] = useState<DrawerMovementType>('Drop');
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));
  const shiftOptions = (shifts ?? []).map((s) => ({ value: s.id, label: `#${s.id} — ${t(`status.${s.status}`, s.status)} — ${new Date(s.openedAtUtc).toLocaleString()}` }));

  const handleAdd = async () => {
    if (shiftId === '' || !amount || Number(amount) <= 0) return;
    try {
      await createMutation.mutateAsync({ shiftId, movementType, amount: Number(amount), reason: reason || undefined });
      showToast(t('drawerMovements.createSuccess'), 'success');
      setAmount(''); setReason('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    try {
      await action();
      showToast(successMessage, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const data: PagedResult<DrawerMovement> = {
    items: movements ?? [], totalCount: movements?.length ?? 0, page: 1, pageSize: Math.max(movements?.length ?? 1, 1)
  };

  const columns: DataGridColumn<DrawerMovement>[] = [
    { key: 'movementType', label: t('drawerMovements.type'), render: (r) => t(`drawerMovements.type${r.movementType}`), exportValue: (r) => r.movementType },
    { key: 'amount', label: t('drawerMovements.amount'), render: (r) => r.amount.toFixed(2), exportValue: (r) => r.amount },
    { key: 'reason', label: t('drawerMovements.reason'), render: (r) => r.reason ?? '—', exportValue: (r) => r.reason ?? '' },
    { key: 'status', label: t('drawerMovements.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status },
    { key: 'createdAtUtc', label: t('drawerMovements.createdAt'), render: (r) => new Date(r.createdAtUtc).toLocaleString(), exportValue: (r) => r.createdAtUtc },
    {
      key: 'actions', label: '',
      render: (r) => r.status === 'Recorded' ? (
        <div style={{ display: 'flex', gap: 6 }}>
          {canApprove && <Button variant="success" size="sm" onClick={() => runAction(() => approveMutation.mutateAsync(r.id), t('drawerMovements.approveSuccess'))}>{t('drawerMovements.approve')}</Button>}
          {canReject && <Button variant="danger" size="sm" onClick={() => runAction(() => rejectMutation.mutateAsync(r.id), t('drawerMovements.rejectSuccess'))}>{t('drawerMovements.reject')}</Button>}
        </div>
      ) : null,
      exportValue: () => ''
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('drawerMovements.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('drawerMovements.terminal')}>
              <SearchableSelect style={{ minWidth: 220 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
            </FieldWrapper>
            <FieldWrapper label={t('drawerMovements.shift')}>
              <SearchableSelect style={{ minWidth: 280 }} value={shiftId} onChange={(v) => setShiftId(v === '' ? '' : Number(v))} options={shiftOptions} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {shiftId !== '' && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('drawerMovements.type')}>
                <SearchableSelect
                  style={{ minWidth: 140 }} value={movementType} onChange={(v) => setMovementType(v as DrawerMovementType)}
                  options={MOVEMENT_TYPES.map((mt) => ({ value: mt, label: t(`drawerMovements.type${mt}`) }))}
                />
              </FieldWrapper>
              <FieldWrapper label={t('drawerMovements.amount')}>
                <Input type="number" step="0.01" style={{ width: 120 }} value={amount} onChange={(e) => setAmount(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('drawerMovements.reason')}>
                <Input style={{ width: 220 }} value={reason} onChange={(e) => setReason(e.target.value)} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleAdd}>{t('drawerMovements.addMovement')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search=""
        onSearchChange={() => {}}
        page={1}
        onPageChange={() => {}}
        exportFileName={t('drawerMovements.title')}
      />
    </div>
  );
}
