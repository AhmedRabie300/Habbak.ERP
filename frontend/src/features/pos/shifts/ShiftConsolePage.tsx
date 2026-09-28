import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useAppStore } from '../../../store/appStore';
import { usePermission } from '../../../ui-kit/usePermission';
import { UserName, useUsersLookup } from '../../settings/security/UserName';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useCloseShift, useOpenShift, useOpenShiftForTerminal } from './api';
import { DenominationGrid, denominationCountsToInput } from './DenominationGrid';

/** /pos/shift-console — screens #1/#3 (05-Module-POS-Shifts.md): فتح/إغلاق وردية على جهاز نقطة
 * بيع. عدّ الفئات إلزامي في الحالتين (قاعدة 1) والإجمالي بيتحدث لحظيًا. الكاشير هو اللي داخل على
 * النظام؛ المشرف (صلاحية الاعتماد) بس يقدر يفتح لكاشير تاني (قاعدة 31). */
export function ShiftConsolePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const { data: terminals } = usePOSTerminalsList();

  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const currentUserId = useAppStore((s) => s.userId);
  const canOverride = usePermission('approve');
  const { data: users } = useUsersLookup();
  const [cashierUserId, setCashierUserId] = useState<number | ''>('');
  const [counts, setCounts] = useState<Record<number, number>>({});

  const { data: openShift, isLoading: isLoadingOpenShift } = useOpenShiftForTerminal(posTerminalId === '' ? undefined : posTerminalId);
  const openMutation = useOpenShift();
  const closeMutation = useCloseShift(openShift?.id ?? 0);

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));

  const handleCountChange = (denominationValue: number, count: number) => {
    setCounts((prev) => ({ ...prev, [denominationValue]: count }));
  };

  const handleOpen = async () => {
    if (posTerminalId === '') return;
    const openingCounts = denominationCountsToInput(counts);
    if (openingCounts.length === 0) {
      showToast(t('shifts.denominationRequired'), 'error');
      return;
    }
    try {
      await openMutation.mutateAsync({ posTerminalId: Number(posTerminalId), cashierUserId: cashierUserId === '' ? undefined : cashierUserId, openingCounts });
      showToast(t('shifts.openSuccess'), 'success');
      setCounts({});
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleClose = async () => {
    if (!openShift) return;
    const closingCounts = denominationCountsToInput(counts);
    if (closingCounts.length === 0) {
      showToast(t('shifts.denominationRequired'), 'error');
      return;
    }
    try {
      await closeMutation.mutateAsync({ rowVersion: openShift.rowVersion, closingCounts });
      showToast(t('shifts.closeSuccess'), 'success');
      setCounts({});
      navigate(`/pos/shifts/${openShift.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <h2 style={{ margin: 0 }}>{t('shifts.consoleTitle')}</h2>

      <ActionBar
        primary={
          openShift
            ? { key: 'close', buttonCode: 'CloseShift', label: t('shifts.close'), onClick: handleClose, disabled: posTerminalId === '' }
            : { key: 'open', label: t('shifts.open'), onClick: handleOpen, disabled: posTerminalId === '' }
        }
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/pos/shifts') }]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('shifts.terminal')}>
              <SearchableSelect
                style={{ minWidth: 220 }}
                value={posTerminalId}
                onChange={(v) => { setPosTerminalId(v === '' ? '' : Number(v)); setCounts({}); }}
                options={terminalOptions}
              />
            </FieldWrapper>
            {!openShift && (
              <FieldWrapper label={t('shifts.cashierUserId')}>
                {canOverride ? (
                  <SearchableSelect
                    id="shift-cashier"
                    style={{ minWidth: 200 }}
                    value={cashierUserId}
                    onChange={(v) => setCashierUserId(v === '' ? '' : Number(v))}
                    options={[
                      { value: '', label: t('shifts.cashierMe') },
                      ...(users ?? []).filter((u) => u.isActive && u.id !== currentUserId).map((u) => ({ value: u.id, label: u.fullName }))
                    ]}
                  />
                ) : (
                  <div style={{ height: 38, display: 'flex', alignItems: 'center', fontWeight: 600 }}><UserName id={currentUserId} /></div>
                )}
              </FieldWrapper>
            )}
          </div>

          {posTerminalId !== '' && isLoadingOpenShift && <div style={{ marginTop: 16 }}>{t('common.loading')}</div>}

          {posTerminalId !== '' && !isLoadingOpenShift && openShift && (
            <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                <StatusBadge status={openShift.status} />
                <span>{t('shifts.cashierUserId')}: <UserName id={openShift.cashierUserId} /></span>
                <span>{t('shifts.openingCashAmount')}: {openShift.openingCashAmount.toFixed(2)}</span>
              </div>
              <h3 style={{ margin: 0 }}>{t('shifts.closingCounts')}</h3>
              <DenominationGrid counts={counts} onChange={handleCountChange} />
            </div>
          )}

          {posTerminalId !== '' && !isLoadingOpenShift && !openShift && (
            <div style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
              <h3 style={{ margin: 0 }}>{t('shifts.openingCounts')}</h3>
              <DenominationGrid counts={counts} onChange={handleCountChange} />
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
