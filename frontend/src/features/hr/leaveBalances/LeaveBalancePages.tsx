import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../employees/api';
import { useLeaveTypes } from '../leaveTypes/api';
import { useAdjustLeaveBalance, useLeaveBalanceHistory, useLeaveBalances, type LeaveBalance } from './api';

const CURRENT_YEAR = new Date().getFullYear();

/** /hr/leave-balances — screen HR_LEAVE_BALANCES. */
export function LeaveBalancesListPage() {
  const { t } = useTranslation();
  const canEdit = usePermission('edit');
  const showToast = useToastStore((s) => s.show);

  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const [year, setYear] = useState(CURRENT_YEAR);
  const { data: employees } = useEmployeesLookup();
  const { data: leaveTypes } = useLeaveTypes();
  const { data: balances, isLoading } = useLeaveBalances({ employeeId: employeeId === '' ? undefined : employeeId, year });

  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;
  const leaveTypeName = (id: number) => leaveTypes?.find((l) => l.id === id)?.nameAr ?? id;

  const [expandedId, setExpandedId] = useState<number | null>(null);
  const { data: history } = useLeaveBalanceHistory(expandedId ?? undefined);

  const [adjustDays, setAdjustDays] = useState(0);
  const [adjustReason, setAdjustReason] = useState('');
  const adjust = useAdjustLeaveBalance();

  const selected = balances?.find((b) => b.id === expandedId);

  const handleAdjust = async () => {
    if (!selected || !adjustReason) return;
    try {
      await adjust.mutateAsync({ employeeId: selected.employeeId, leaveTypeId: selected.leaveTypeId, year: selected.year, days: adjustDays, reason: adjustReason });
      showToast(t('hr.attendance.leaveBalances.adjustSuccess'), 'success');
      setAdjustDays(0);
      setAdjustReason('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<LeaveBalance>[] = useMemo(
    () => [
      { key: 'employeeId', label: t('hr.attendance.leaveBalances.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
      { key: 'leaveTypeId', label: t('hr.attendance.leaveBalances.leaveType'), render: (r) => leaveTypeName(r.leaveTypeId), exportValue: (r) => leaveTypeName(r.leaveTypeId) },
      { key: 'year', label: t('hr.attendance.leaveBalances.year'), render: (r) => r.year, exportValue: (r) => r.year },
      { key: 'accruedThisYear', label: t('hr.attendance.leaveBalances.accrued'), render: (r) => r.accruedThisYear, exportValue: (r) => r.accruedThisYear },
      { key: 'carriedOver', label: t('hr.attendance.leaveBalances.carriedOver'), render: (r) => r.carriedOver, exportValue: (r) => r.carriedOver },
      { key: 'used', label: t('hr.attendance.leaveBalances.used'), render: (r) => r.used, exportValue: (r) => r.used },
      { key: 'pending', label: t('hr.attendance.leaveBalances.pending'), render: (r) => r.pending, exportValue: (r) => r.pending },
      { key: 'available', label: t('hr.attendance.leaveBalances.available'), render: (r) => r.available, exportValue: (r) => r.available }
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, employees, leaveTypes]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.leaveBalances.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <FieldWrapper label={t('hr.attendance.leaveBalances.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => { setEmployeeId(v === '' ? '' : Number(v)); setExpandedId(null); }}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveBalances.year')}>
              <Input type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      <DataGrid
        columns={columns}
        data={{ items: balances ?? [], totalCount: (balances ?? []).length, page: 1, pageSize: Math.max((balances ?? []).length, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => setExpandedId(row.id === expandedId ? null : row.id)}
        exportFileName={t('hr.attendance.leaveBalances.title')}
      />

      {selected && (
        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{t('hr.attendance.leaveBalances.history')} — {employeeName(selected.employeeId)} / {leaveTypeName(selected.leaveTypeId)}</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.leaveBalances.effectiveDate')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.leaveBalances.movementType')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.leaveBalances.days')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.attendance.leaveBalances.reason')}</th>
                </tr>
              </thead>
              <tbody>
                {(history ?? []).map((h) => (
                  <tr key={h.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                    <td style={{ padding: 6 }}>{h.effectiveDate}</td>
                    <td style={{ padding: 6 }}>{t(`hr.attendance.leaveBalances.movementTypes.${h.movementType}`)}</td>
                    <td style={{ padding: 6 }}>{h.days}</td>
                    <td style={{ padding: 6 }}>{h.reason ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {canEdit && (
              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end', marginTop: 16 }}>
                <strong>{t('hr.attendance.leaveBalances.adjust')}</strong>
                <FieldWrapper label={t('hr.attendance.leaveBalances.adjustDays')}>
                  <Input type="number" value={adjustDays} onChange={(e) => setAdjustDays(Number(e.target.value))} style={{ width: 100 }} />
                </FieldWrapper>
                <FieldWrapper label={t('hr.attendance.leaveBalances.adjustReason')}>
                  <Input value={adjustReason} onChange={(e) => setAdjustReason(e.target.value)} style={{ minWidth: 220 }} />
                </FieldWrapper>
                <Button variant="primary" onClick={handleAdjust} disabled={!adjustReason || adjustDays === 0}>{t('common.saveChanges')}</Button>
              </div>
            )}
          </CardBody>
        </Card>
      )}
    </div>
  );
}
