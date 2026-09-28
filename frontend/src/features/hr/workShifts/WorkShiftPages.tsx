import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useCodingRule } from '../../settings/codingRules/api';
import { toPaged } from '../listing';
import { useDeleteWorkShiftDefinition, useSaveWorkShiftDefinition, useWorkShiftDefinitions, type WorkShiftDefinition } from './api';

/** `<input type="time">` بيرجّع "HH:mm" — الـ Backend (System.Text.Json `TimeOnly`) محتاج "HH:mm:ss". */
function toTimeOnly(hhmm: string): string {
  return hhmm.length === 5 ? `${hhmm}:00` : hhmm;
}

/** /hr/work-shifts — screen HR_WORK_SHIFTS. */
export function WorkShiftsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useWorkShiftDefinitions();

  const rows = toPaged(data, search, (w, q) => `${w.code} ${w.nameAr} ${w.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<WorkShiftDefinition>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'startTime', label: t('hr.attendance.workShifts.startTime'), render: (r) => r.startTime, exportValue: (r) => r.startTime },
    { key: 'endTime', label: t('hr.attendance.workShifts.endTime'), render: (r) => r.endTime, exportValue: (r) => r.endTime },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.workShifts.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/work-shifts/new')}>{t('hr.attendance.workShifts.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/work-shifts/${row.id}`)}
        exportFileName={t('hr.attendance.workShifts.title')}
      />
    </div>
  );
}

/** /hr/work-shifts/:id */
export function WorkShiftEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const shiftId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: shifts, isLoading } = useWorkShiftDefinitions();
  const shift = shifts?.find((w) => w.id === shiftId);
  const { data: codingRule } = useCodingRule('HR_WORK_SHIFTS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [startTime, setStartTime] = useState('08:00');
  const [endTime, setEndTime] = useState('16:00');
  const [breakMinutes, setBreakMinutes] = useState(0);
  const [isNightShift, setIsNightShift] = useState(false);
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!shift) return;
    setNameAr(shift.nameAr);
    setNameEn(shift.nameEn);
    setStartTime(shift.startTime.slice(0, 5));
    setEndTime(shift.endTime.slice(0, 5));
    setBreakMinutes(shift.breakMinutes);
    setIsNightShift(shift.isNightShift);
    setIsActive(shift.isActive);
  }, [shift]);

  const save = useSaveWorkShiftDefinition(shiftId);
  const remove = useDeleteWorkShiftDefinition();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: { nameAr, nameEn, startTime: toTimeOnly(startTime), endTime: toTimeOnly(endTime), breakMinutes, isNightShift, isActive }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/work-shifts/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!shiftId) return;
    try {
      await remove.mutateAsync(shiftId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/work-shifts');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.attendance.workShifts.add') : `${t('hr.attendance.workShifts.title')} — ${shift?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/work-shifts') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.attendance.workShifts.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (shift?.code ?? '')}
                onChange={(e) => setCode(e.target.value)}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameAr')}>
              <Input value={nameAr} onChange={(e) => setNameAr(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameEn')}>
              <Input value={nameEn} onChange={(e) => setNameEn(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.workShifts.startTime')}>
              <Input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.workShifts.endTime')}>
              <Input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.workShifts.breakMinutes')}>
              <Input type="number" value={breakMinutes} onChange={(e) => setBreakMinutes(Number(e.target.value))} style={{ width: 120 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.workShifts.isNightShift')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isNightShift} onChange={(e) => setIsNightShift(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.isActive')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              </label>
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
