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
import { useLeaveTypes } from '../leaveTypes/api';
import {
  useCalculateLeaveDays, useCancelLeaveRequest, useCreateLeaveRequest, useLeaveRequests, useSubmitLeaveRequest, type LeaveRequest
} from './api';

/** /hr/leave-requests — screen HR_LEAVE_REQUESTS. */
export function LeaveRequestsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const canEdit = usePermission('edit');
  const showToast = useToastStore((s) => s.show);

  const { data: requests, isLoading } = useLeaveRequests();
  const { data: employees } = useEmployeesLookup();
  const { data: leaveTypes } = useLeaveTypes();
  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;
  const leaveTypeName = (id: number) => leaveTypes?.find((l) => l.id === id)?.nameAr ?? id;

  const submit = useSubmitLeaveRequest();
  const cancel = useCancelLeaveRequest();

  const handleSubmit = async (id: number) => {
    try {
      await submit.mutateAsync(id);
      showToast(t('hr.attendance.leaveRequests.submitSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCancel = async (id: number) => {
    try {
      await cancel.mutateAsync(id);
      showToast(t('hr.attendance.leaveRequests.cancelSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<LeaveRequest>[] = useMemo(
    () => [
      { key: 'employeeId', label: t('hr.attendance.leaveRequests.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
      { key: 'leaveTypeId', label: t('hr.attendance.leaveRequests.leaveType'), render: (r) => leaveTypeName(r.leaveTypeId), exportValue: (r) => leaveTypeName(r.leaveTypeId) },
      { key: 'startDate', label: t('hr.attendance.leaveRequests.startDate'), render: (r) => r.startDate, exportValue: (r) => r.startDate },
      { key: 'endDate', label: t('hr.attendance.leaveRequests.endDate'), render: (r) => r.endDate, exportValue: (r) => r.endDate },
      { key: 'days', label: t('hr.attendance.leaveRequests.days'), render: (r) => r.days, exportValue: (r) => r.days },
      { key: 'status', label: t('hr.attendance.leaveRequests.status'), render: (r) => t(`hr.attendance.leaveRequests.statuses.${r.status}`), exportValue: (r) => r.status },
      {
        key: 'actions',
        label: '',
        render: (r) =>
          canEdit ? (
            <div style={{ display: 'flex', gap: 6 }} onClick={(e) => e.stopPropagation()}>
              {r.status === 'Draft' && <Button variant="secondary" onClick={() => handleSubmit(r.id)}>{t('hr.attendance.leaveRequests.submit')}</Button>}
              {(r.status === 'Draft' || r.status === 'Pending' || r.status === 'Approved') && (
                <Button variant="secondary" onClick={() => handleCancel(r.id)}>{t('hr.attendance.leaveRequests.cancel')}</Button>
              )}
            </div>
          ) : null
      }
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, employees, leaveTypes, canEdit]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.leaveRequests.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/leave-requests/new')}>{t('hr.attendance.leaveRequests.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={{ items: requests ?? [], totalCount: (requests ?? []).length, page: 1, pageSize: Math.max((requests ?? []).length, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        exportFileName={t('hr.attendance.leaveRequests.title')}
      />
    </div>
  );
}

/** /hr/leave-requests/new — إنشاء بس (زي أغلب المستندات، مفيش تعديل بعد الإنشاء). */
export function LeaveRequestEditPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: employees } = useEmployeesLookup();
  const { data: leaveTypes } = useLeaveTypes();

  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const [leaveTypeId, setLeaveTypeId] = useState<number | ''>('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [reason, setReason] = useState('');

  const { data: preview } = useCalculateLeaveDays(employeeId, startDate, endDate);
  const create = useCreateLeaveRequest();

  const handleSave = async () => {
    if (employeeId === '' || leaveTypeId === '' || !startDate || !endDate) return;
    try {
      const result = await create.mutateAsync({ employeeId, leaveTypeId, startDate, endDate, reason: reason || null });
      showToast(t('hr.saveSuccess'), 'success');
      navigate('/hr/leave-requests');
      void result;
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.leaveRequests.add')}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/leave-requests') }]}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.attendance.leaveRequests.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveRequests.leaveType')}>
              <SearchableSelect
                value={leaveTypeId}
                onChange={(v) => setLeaveTypeId(v === '' ? '' : Number(v))}
                options={(leaveTypes ?? []).map((l) => ({ value: l.id, label: l.nameAr }))}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveRequests.startDate')}>
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveRequests.endDate')}>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveRequests.reason')}>
              <Input value={reason} onChange={(e) => setReason(e.target.value)} style={{ minWidth: 260 }} />
            </FieldWrapper>
          </div>

          {preview && (
            <div style={{ marginTop: 12 }}>
              <strong>{t('hr.attendance.leaveRequests.calculatedDays', { days: preview.days })}</strong>
              {preview.usedFallback && (
                <div style={{ color: 'var(--color-error)', marginTop: 4 }}>{t('hr.attendance.leaveRequests.usedFallbackWarning')}</div>
              )}
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
