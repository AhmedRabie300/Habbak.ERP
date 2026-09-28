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
import { useCodingRule } from '../../settings/codingRules/api';
import { useBranchesList } from '../../organization/branches/api';
import { toPaged } from '../listing';
import { useDeleteHoliday, useHolidays, useSaveHoliday, type Holiday } from './api';

/** /hr/holidays — screen HR_HOLIDAYS. */
export function HolidaysListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useHolidays();

  const rows = toPaged(data, search, (h, q) => `${h.code} ${h.nameAr} ${h.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<Holiday>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'startDate', label: t('hr.attendance.holidays.startDate'), render: (r) => r.startDate, exportValue: (r) => r.startDate },
    { key: 'endDate', label: t('hr.attendance.holidays.endDate'), render: (r) => r.endDate ?? '—', exportValue: (r) => r.endDate ?? '' },
    { key: 'isNational', label: t('hr.attendance.holidays.isNational'), render: (r) => (r.isNational ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isNational ? t('common.yes') : t('common.no')) },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.holidays.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/holidays/new')}>{t('hr.attendance.holidays.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/holidays/${row.id}`)}
        exportFileName={t('hr.attendance.holidays.title')}
      />
    </div>
  );
}

/** /hr/holidays/:id */
export function HolidayEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const holidayId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: holidays, isLoading } = useHolidays();
  const holiday = holidays?.find((h) => h.id === holidayId);
  const { data: codingRule } = useCodingRule('HR_HOLIDAYS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;
  const { data: branches } = useBranchesList();
  const branchOptions = useMemo(
    () => [{ value: '', label: t('hr.attendance.holidays.allBranches') }, ...(branches ?? []).map((b) => ({ value: b.id, label: b.nameAr }))],
    [branches, t]
  );

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [isNational, setIsNational] = useState(true);
  const [branchId, setBranchId] = useState<number | ''>('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!holiday) return;
    setNameAr(holiday.nameAr);
    setNameEn(holiday.nameEn);
    setStartDate(holiday.startDate.slice(0, 10));
    setEndDate(holiday.endDate?.slice(0, 10) ?? '');
    setIsNational(holiday.isNational);
    setBranchId(holiday.branchId ?? '');
    setIsActive(holiday.isActive);
  }, [holiday]);

  const save = useSaveHoliday(holidayId);
  const remove = useDeleteHoliday();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: { nameAr, nameEn, startDate, endDate: endDate || null, isNational, branchId: branchId === '' ? null : Number(branchId), isActive }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/holidays/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!holidayId) return;
    try {
      await remove.mutateAsync(holidayId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/holidays');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.attendance.holidays.add') : `${t('hr.attendance.holidays.title')} — ${holiday?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/holidays') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.attendance.holidays.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (holiday?.code ?? '')}
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
            <FieldWrapper label={t('hr.attendance.holidays.startDate')}>
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.holidays.endDate')}>
              <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} min={startDate || undefined} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.holidays.isNational')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isNational} onChange={(e) => setIsNational(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.holidays.branch')}>
              <SearchableSelect value={branchId} onChange={(v) => setBranchId(v === '' ? '' : Number(v))} options={branchOptions} style={{ minWidth: 200 }} />
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
