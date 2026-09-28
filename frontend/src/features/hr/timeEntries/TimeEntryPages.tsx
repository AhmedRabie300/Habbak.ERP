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
import {
  useAcceptTimeEntry, useCorrectTimeEntry, useCreateTimeEntry, useDismissTimeEntry, useTimeEntries,
  type TimeEntry, type TimeEntryType
} from './api';

/** /hr/time-entries — screen HR_TIME_ENTRIES. */
export function TimeEntriesListPage() {
  const { t } = useTranslation();
  const canAdd = usePermission('add');
  const canEdit = usePermission('edit');
  const showToast = useToastStore((s) => s.show);

  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const { data: employees } = useEmployeesLookup();
  const { data: entries, isLoading } = useTimeEntries({ employeeId: employeeId === '' ? undefined : employeeId });
  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;

  const accept = useAcceptTimeEntry();
  const dismiss = useDismissTimeEntry();
  const correct = useCorrectTimeEntry();
  const create = useCreateTimeEntry();

  const [newEmployeeId, setNewEmployeeId] = useState<number | ''>('');
  const [newEntryType, setNewEntryType] = useState<TimeEntryType>('In');
  const [newTimestamp, setNewTimestamp] = useState('');

  const [correctingId, setCorrectingId] = useState<number | null>(null);
  const [correctTimestamp, setCorrectTimestamp] = useState('');
  const [correctType, setCorrectType] = useState<TimeEntryType>('In');
  const [correctReason, setCorrectReason] = useState('');

  const handleCreate = async () => {
    if (newEmployeeId === '' || !newTimestamp) return;
    try {
      await create.mutateAsync({ employeeId: newEmployeeId, entryType: newEntryType, timestampUtc: newTimestamp });
      showToast(t('hr.saveSuccess'), 'success');
      setNewTimestamp('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleAccept = async (id: number) => {
    try {
      await accept.mutateAsync(id);
      showToast(t('hr.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDismiss = async (id: number) => {
    try {
      await dismiss.mutateAsync(id);
      showToast(t('hr.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCorrect = async () => {
    if (correctingId === null || !correctTimestamp || !correctReason) return;
    try {
      await correct.mutateAsync({ id: correctingId, entryType: correctType, timestampUtc: correctTimestamp, reason: correctReason });
      showToast(t('hr.attendance.timeEntries.correctSuccess'), 'success');
      setCorrectingId(null);
      setCorrectReason('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<TimeEntry>[] = useMemo(
    () => [
      { key: 'employeeId', label: t('hr.attendance.timeEntries.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
      { key: 'entryType', label: t('hr.attendance.timeEntries.entryType'), render: (r) => t(`hr.attendance.timeEntries.entryTypes.${r.entryType}`), exportValue: (r) => r.entryType },
      { key: 'timestampUtc', label: t('hr.attendance.timeEntries.timestamp'), render: (r) => new Date(r.timestampUtc).toLocaleString(), exportValue: (r) => r.timestampUtc },
      { key: 'source', label: t('hr.attendance.timeEntries.source'), render: (r) => t(`hr.attendance.timeEntries.sources.${r.source}`), exportValue: (r) => r.source },
      { key: 'status', label: t('hr.attendance.timeEntries.status'), render: (r) => t(`hr.attendance.timeEntries.statuses.${r.status}`), exportValue: (r) => r.status },
      {
        key: 'actions',
        label: t('common.actions', { defaultValue: 'إجراءات' }),
        render: (r) => (
          <div style={{ display: 'flex', gap: 6 }} onClick={(e) => e.stopPropagation()}>
            {canEdit && r.status === 'Suggested' && (
              <>
                <Button variant="secondary" onClick={() => handleAccept(r.id)}>{t('hr.attendance.timeEntries.accept')}</Button>
                <Button variant="secondary" onClick={() => handleDismiss(r.id)}>{t('hr.attendance.timeEntries.dismiss')}</Button>
              </>
            )}
            {canEdit && (
              <Button
                variant="secondary"
                onClick={() => {
                  setCorrectingId(r.id);
                  setCorrectType(r.entryType);
                  setCorrectTimestamp(r.timestampUtc.slice(0, 16));
                }}
              >
                {t('hr.attendance.timeEntries.correct')}
              </Button>
            )}
          </div>
        )
      }
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, employees, canEdit]
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('hr.attendance.timeEntries.title')}</h2>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <FieldWrapper label={t('hr.attendance.timeEntries.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>

      {canAdd && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
              <strong>{t('hr.attendance.timeEntries.add')}</strong>
              <FieldWrapper label={t('hr.attendance.timeEntries.employee')}>
                <SearchableSelect
                  value={newEmployeeId}
                  onChange={(v) => setNewEmployeeId(v === '' ? '' : Number(v))}
                  options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                  style={{ minWidth: 220 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.timeEntries.entryType')}>
                <SearchableSelect
                  value={newEntryType}
                  onChange={(v) => setNewEntryType(v as TimeEntryType)}
                  options={[{ value: 'In', label: t('hr.attendance.timeEntries.entryTypes.In') }, { value: 'Out', label: t('hr.attendance.timeEntries.entryTypes.Out') }]}
                  style={{ minWidth: 120 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.timeEntries.timestamp')}>
                <Input type="datetime-local" value={newTimestamp} onChange={(e) => setNewTimestamp(e.target.value)} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleCreate} disabled={newEmployeeId === '' || !newTimestamp}>{t('common.saveChanges')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      {correctingId !== null && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
              <strong>{t('hr.attendance.timeEntries.correct')}</strong>
              <FieldWrapper label={t('hr.attendance.timeEntries.entryType')}>
                <SearchableSelect
                  value={correctType}
                  onChange={(v) => setCorrectType(v as TimeEntryType)}
                  options={[{ value: 'In', label: t('hr.attendance.timeEntries.entryTypes.In') }, { value: 'Out', label: t('hr.attendance.timeEntries.entryTypes.Out') }]}
                  style={{ minWidth: 120 }}
                />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.timeEntries.timestamp')}>
                <Input type="datetime-local" value={correctTimestamp} onChange={(e) => setCorrectTimestamp(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.attendance.timeEntries.correctReason')}>
                <Input value={correctReason} onChange={(e) => setCorrectReason(e.target.value)} style={{ minWidth: 220 }} />
              </FieldWrapper>
              <Button variant="primary" onClick={handleCorrect} disabled={!correctTimestamp || !correctReason}>{t('common.saveChanges')}</Button>
              <Button variant="secondary" onClick={() => setCorrectingId(null)}>{t('common.back')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      <DataGrid
        columns={columns}
        data={{ items: entries ?? [], totalCount: (entries ?? []).length, page: 1, pageSize: Math.max((entries ?? []).length, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        exportFileName={t('hr.attendance.timeEntries.title')}
      />
    </div>
  );
}
