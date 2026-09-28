import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useEmployeesLookup } from '../employees/api';
import {
  useCancelOvertimeRequest, useCreateOvertimeRequest, useOvertimeRequests, useSubmitOvertimeRequest, type OvertimeRequest, type OvertimeType
} from './api';

const OVERTIME_TYPES: OvertimeType[] = ['Day', 'Night', 'RestDay', 'PublicHoliday'];

/** /hr/overtime-requests — screen HR_OVERTIME. */
export function OvertimeRequestsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const canEdit = usePermission('edit');
  const showToast = useToastStore((s) => s.show);

  const { data: requests, isLoading } = useOvertimeRequests();
  const { data: employees } = useEmployeesLookup();
  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;

  const submit = useSubmitOvertimeRequest();
  const cancel = useCancelOvertimeRequest();

  const handleSubmit = async (id: number) => {
    try {
      await submit.mutateAsync(id);
      showToast(t('hr.attendance.overtime.submitSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCancel = async (id: number) => {
    try {
      await cancel.mutateAsync(id);
      showToast(t('hr.attendance.overtime.cancelSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<OvertimeRequest>[] = useMemo(
    () => [
      { key: 'employeeId', label: t('hr.attendance.overtime.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
      { key: 'workDate', label: t('hr.attendance.overtime.workDate'), render: (r) => r.workDate, exportValue: (r) => r.workDate },
      { key: 'plannedMinutes', label: t('hr.attendance.overtime.plannedMinutes'), render: (r) => r.plannedMinutes, exportValue: (r) => r.plannedMinutes },
      { key: 'overtimeType', label: t('hr.attendance.overtime.overtimeType'), render: (r) => t(`hr.attendance.overtime.overtimeTypes.${r.overtimeType}`), exportValue: (r) => r.overtimeType },
      { key: 'status', label: t('hr.attendance.overtime.status'), render: (r) => t(`hr.attendance.leaveRequests.statuses.${r.status}`), exportValue: (r) => r.status },
      {
        key: 'actions',
        label: '',
        render: (r) =>
          canEdit ? (
            <div style={{ display: 'flex', gap: 6 }} onClick={(e) => e.stopPropagation()}>
              {r.status === 'Draft' && <Button variant="secondary" onClick={() => handleSubmit(r.id)}>{t('hr.attendance.overtime.submit')}</Button>}
              {(r.status === 'Draft' || r.status === 'Pending' || r.status === 'Approved') && (
                <Button variant="secondary" onClick={() => handleCancel(r.id)}>{t('hr.attendance.overtime.cancel')}</Button>
              )}
            </div>
          ) : null
      }
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, employees, canEdit]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.overtime.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/overtime-requests/new')}>{t('hr.attendance.overtime.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={{ items: requests ?? [], totalCount: (requests ?? []).length, page: 1, pageSize: Math.max((requests ?? []).length, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        exportFileName={t('hr.attendance.overtime.title')}
      />
    </div>
  );
}

/** /hr/overtime-requests/new */
export function OvertimeRequestEditPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const { data: employees } = useEmployeesLookup();

  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const [workDate, setWorkDate] = useState('');
  const [plannedMinutes, setPlannedMinutes] = useState(60);
  const [overtimeType, setOvertimeType] = useState<OvertimeType>('Day');
  const [reason, setReason] = useState('');

  const create = useCreateOvertimeRequest();

  const handleSave = async () => {
    if (employeeId === '' || !workDate) return;
    try {
      await create.mutateAsync({ employeeId, workDate, plannedMinutes, overtimeType, reason: reason || null });
      showToast(t('hr.saveSuccess'), 'success');
      navigate('/hr/overtime-requests');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.overtime.add')}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/overtime-requests') }]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.attendance.overtime.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.overtime.workDate')}>
              <Input type="date" value={workDate} onChange={(e) => setWorkDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.overtime.plannedMinutes')}>
              <Input type="number" value={plannedMinutes} onChange={(e) => setPlannedMinutes(Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.overtime.overtimeType')}>
              <SearchableSelect
                value={overtimeType}
                onChange={(v) => setOvertimeType(v as OvertimeType)}
                options={OVERTIME_TYPES.map((ty) => ({ value: ty, label: t(`hr.attendance.overtime.overtimeTypes.${ty}`) }))}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.overtime.reason')}>
              <Input value={reason} onChange={(e) => setReason(e.target.value)} style={{ minWidth: 260 }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
