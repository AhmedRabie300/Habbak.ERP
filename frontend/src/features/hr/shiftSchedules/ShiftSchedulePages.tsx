import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
import { useWorkShiftDefinitions } from '../workShifts/api';
import {
  useCreateShiftSchedule, useDeleteShiftSchedule, useGenerateWeeklySchedule, useShiftSchedules, useUpdateShiftSchedule, type ShiftSchedule
} from './api';

const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/** /hr/shift-schedules — screen HR_SHIFT_SCHEDULES. */
export function ShiftSchedulesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const { data: employees } = useEmployeesLookup();
  const { data: schedules, isLoading } = useShiftSchedules({ employeeId: employeeId === '' ? undefined : employeeId });
  const { data: workShifts } = useWorkShiftDefinitions();
  const showToast = useToastStore((s) => s.show);

  const employeeOptions = useMemo(() => (employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` })), [employees]);
  const employeeName = (id: number) => employees?.find((e) => e.id === id)?.nameAr ?? id;
  const workShiftName = (id: number | null) => (id ? workShifts?.find((w) => w.id === id)?.nameAr ?? '—' : '—');

  const [weekStartDate, setWeekStartDate] = useState('');
  const [genWorkShiftId, setGenWorkShiftId] = useState<number | ''>('');
  const [restDay, setRestDay] = useState<string>('Friday');
  const generateWeekly = useGenerateWeeklySchedule();

  const handleGenerate = async () => {
    if (employeeId === '' || !weekStartDate) return;
    try {
      const result = await generateWeekly.mutateAsync({
        employeeId, weekStartDate, workShiftDefinitionId: genWorkShiftId === '' ? null : Number(genWorkShiftId), restDayOfWeek: restDay || null
      });
      showToast(t('hr.attendance.shiftSchedules.generatedCount', { count: result.created }), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<ShiftSchedule>[] = [
    { key: 'employeeId', label: t('hr.attendance.shiftSchedules.employee'), render: (r) => employeeName(r.employeeId), exportValue: (r) => employeeName(r.employeeId) },
    { key: 'startDate', label: t('hr.attendance.shiftSchedules.startDate'), render: (r) => r.startDate, exportValue: (r) => r.startDate },
    { key: 'endDate', label: t('hr.attendance.shiftSchedules.endDate'), render: (r) => r.endDate ?? '—', exportValue: (r) => r.endDate ?? '' },
    { key: 'workShift', label: t('hr.attendance.shiftSchedules.workShift'), render: (r) => workShiftName(r.workShiftDefinitionId), exportValue: (r) => workShiftName(r.workShiftDefinitionId) },
    { key: 'isRestDay', label: t('hr.attendance.shiftSchedules.isRestDay'), render: (r) => (r.isRestDay ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isRestDay ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.shiftSchedules.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          {canAdd && (
            <Button variant="secondary" onClick={() => navigate('/hr/shift-schedule-generator')}>
              {t('hr.attendance.shiftSchedules.bulkGenerator')}
            </Button>
          )}
          {canAdd && <Button variant="primary" onClick={() => navigate('/hr/shift-schedules/new')}>{t('hr.attendance.shiftSchedules.add')}</Button>}
        </div>
      </div>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.employee')}>
              <SearchableSelect value={employeeId} onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))} options={employeeOptions} style={{ minWidth: 220 }} />
            </FieldWrapper>
            {canAdd && (
              <>
                <FieldWrapper label={t('hr.attendance.shiftSchedules.generateWeekly')}>
                  <span />
                </FieldWrapper>
                <FieldWrapper label={t('hr.attendance.shiftSchedules.weekStartDate')}>
                  <Input type="date" value={weekStartDate} onChange={(e) => setWeekStartDate(e.target.value)} />
                </FieldWrapper>
                <FieldWrapper label={t('hr.attendance.shiftSchedules.workShift')}>
                  <SearchableSelect
                    value={genWorkShiftId}
                    onChange={(v) => setGenWorkShiftId(v === '' ? '' : Number(v))}
                    options={[{ value: '', label: t('hr.attendance.shiftSchedules.noWorkShift') }, ...(workShifts ?? []).map((w) => ({ value: w.id, label: w.nameAr }))]}
                    style={{ minWidth: 180 }}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('hr.attendance.shiftSchedules.restDayOfWeek')}>
                  <SearchableSelect value={restDay} onChange={setRestDay} options={WEEKDAYS.map((d) => ({ value: d, label: d }))} style={{ minWidth: 140 }} />
                </FieldWrapper>
                <Button variant="secondary" onClick={handleGenerate} disabled={employeeId === '' || !weekStartDate}>{t('hr.attendance.shiftSchedules.generate')}</Button>
              </>
            )}
          </div>
        </CardBody>
      </Card>

      <DataGrid
        columns={columns}
        data={{ items: schedules ?? [], totalCount: (schedules ?? []).length, page: 1, pageSize: Math.max((schedules ?? []).length, 1) }}
        isLoading={isLoading}
        search=""
        onSearchChange={() => undefined}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/shift-schedules/${row.id}`)}
        exportFileName={t('hr.attendance.shiftSchedules.title')}
      />
    </div>
  );
}

/** /hr/shift-schedules/:id — new أو تعديل فترة (Remarks8 Item 5 — From/To، مش يوم واحد بس). */
export function ShiftScheduleEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const scheduleId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: schedules, isLoading } = useShiftSchedules({});
  const schedule = schedules?.find((s) => s.id === scheduleId);
  const { data: employees } = useEmployeesLookup();
  const { data: workShifts } = useWorkShiftDefinitions();

  const [employeeId, setEmployeeId] = useState<number | ''>('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [workShiftDefinitionId, setWorkShiftDefinitionId] = useState<number | ''>('');
  const [isRestDay, setIsRestDay] = useState(false);

  useEffect(() => {
    if (!schedule) return;
    setEmployeeId(schedule.employeeId);
    setStartDate(schedule.startDate.slice(0, 10));
    setEndDate(schedule.endDate?.slice(0, 10) ?? '');
    setWorkShiftDefinitionId(schedule.workShiftDefinitionId ?? '');
    setIsRestDay(schedule.isRestDay);
  }, [schedule]);

  const create = useCreateShiftSchedule();
  const update = useUpdateShiftSchedule(scheduleId);
  const remove = useDeleteShiftSchedule();

  const handleSave = async () => {
    if (employeeId === '' || !startDate) return;
    try {
      if (isNew) {
        const result = await create.mutateAsync({
          employeeId, startDate, endDate: endDate || null, workShiftDefinitionId: workShiftDefinitionId === '' ? null : Number(workShiftDefinitionId), isRestDay
        });
        showToast(t('hr.saveSuccess'), 'success');
        navigate(`/hr/shift-schedules/${result.id}`);
      } else {
        await update.mutateAsync({
          startDate, endDate: endDate || null, workShiftDefinitionId: workShiftDefinitionId === '' ? null : Number(workShiftDefinitionId), isRestDay
        });
        showToast(t('hr.saveSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!scheduleId) return;
    try {
      await remove.mutateAsync(scheduleId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/shift-schedules');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.attendance.shiftSchedules.add') : t('hr.attendance.shiftSchedules.title')}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/shift-schedules') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.attendance.shiftSchedules.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.employee')}>
              <SearchableSelect
                value={employeeId}
                onChange={(v) => setEmployeeId(v === '' ? '' : Number(v))}
                options={(employees ?? []).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))}
                disabled={!isNew}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.startDate')}>
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.endDate')}>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} min={startDate || undefined} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.workShift')}>
              <SearchableSelect
                value={workShiftDefinitionId}
                onChange={(v) => setWorkShiftDefinitionId(v === '' ? '' : Number(v))}
                options={[{ value: '', label: t('hr.attendance.shiftSchedules.noWorkShift') }, ...(workShifts ?? []).map((w) => ({ value: w.id, label: w.nameAr }))]}
                style={{ minWidth: 200 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.shiftSchedules.isRestDay')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isRestDay} onChange={(e) => setIsRestDay(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
