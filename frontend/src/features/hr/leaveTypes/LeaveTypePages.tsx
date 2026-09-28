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
import { toPaged } from '../listing';
import { useDeleteLeaveType, useLeaveTypes, useSaveLeaveType, type Gender, type LeaveAccrualMethod, type LeaveType } from './api';

const ACCRUAL_METHODS: LeaveAccrualMethod[] = ['Monthly', 'Annual', 'Hourly', 'None'];
const GENDERS: Gender[] = ['Male', 'Female'];

/** /hr/leave-types — screen HR_LEAVE_TYPES. */
export function LeaveTypesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useLeaveTypes();

  const rows = toPaged(data, search, (l, q) => `${l.code} ${l.nameAr} ${l.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<LeaveType>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'accrualMethod', label: t('hr.attendance.leaveTypes.accrualMethod'), render: (r) => t(`hr.attendance.leaveTypes.accrualMethods.${r.accrualMethod}`), exportValue: (r) => r.accrualMethod },
    { key: 'annualDays', label: t('hr.attendance.leaveTypes.annualDays'), render: (r) => r.annualDays, exportValue: (r) => r.annualDays },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.attendance.leaveTypes.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/hr/leave-types/new')}>{t('hr.attendance.leaveTypes.add')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/leave-types/${row.id}`)}
        exportFileName={t('hr.attendance.leaveTypes.title')}
      />
    </div>
  );
}

/** /hr/leave-types/:id */
export function LeaveTypeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const leaveTypeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: leaveTypes, isLoading } = useLeaveTypes();
  const leaveType = leaveTypes?.find((l) => l.id === leaveTypeId);
  const { data: codingRule } = useCodingRule('HR_LEAVE_TYPES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const deductFromOptions = useMemo(
    () => [{ value: '', label: t('hr.attendance.leaveTypes.noDeductFrom') }, ...(leaveTypes ?? []).filter((l) => l.id !== leaveTypeId).map((l) => ({ value: l.id, label: l.nameAr }))],
    [leaveTypes, leaveTypeId, t]
  );
  const genderOptions = useMemo(
    () => [{ value: '', label: t('hr.attendance.leaveTypes.noGenderRestriction') }, ...GENDERS.map((g) => ({ value: g, label: t(`hr.employees.genders.${g}`) }))],
    [t]
  );

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [accrualMethod, setAccrualMethod] = useState<LeaveAccrualMethod>('Monthly');
  const [annualDays, setAnnualDays] = useState(21);
  const [maxCarryOver, setMaxCarryOver] = useState(0);
  const [isPaid, setIsPaid] = useState(true);
  const [paidPercentage, setPaidPercentage] = useState(100);
  const [deductFromLeaveTypeId, setDeductFromLeaveTypeId] = useState<number | ''>('');
  const [requiresDocument, setRequiresDocument] = useState(false);
  const [maxDaysPerRequest, setMaxDaysPerRequest] = useState<number | ''>('');
  const [genderRestriction, setGenderRestriction] = useState<Gender | ''>('');
  const [maxTimesInService, setMaxTimesInService] = useState<number | ''>('');
  const [isCashableOnTermination, setIsCashableOnTermination] = useState(false);
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!leaveType) return;
    setNameAr(leaveType.nameAr);
    setNameEn(leaveType.nameEn);
    setAccrualMethod(leaveType.accrualMethod);
    setAnnualDays(leaveType.annualDays);
    setMaxCarryOver(leaveType.maxCarryOver);
    setIsPaid(leaveType.isPaid);
    setPaidPercentage(leaveType.paidPercentage);
    setDeductFromLeaveTypeId(leaveType.deductFromLeaveTypeId ?? '');
    setRequiresDocument(leaveType.requiresDocument);
    setMaxDaysPerRequest(leaveType.maxDaysPerRequest ?? '');
    setGenderRestriction(leaveType.genderRestriction ?? '');
    setMaxTimesInService(leaveType.maxTimesInService ?? '');
    setIsCashableOnTermination(leaveType.isCashableOnTermination);
    setIsActive(leaveType.isActive);
  }, [leaveType]);

  const save = useSaveLeaveType(leaveTypeId);
  const remove = useDeleteLeaveType();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr, nameEn, accrualMethod, annualDays, maxCarryOver, isPaid, paidPercentage,
          deductFromLeaveTypeId: deductFromLeaveTypeId === '' ? null : Number(deductFromLeaveTypeId),
          requiresDocument,
          maxDaysPerRequest: maxDaysPerRequest === '' ? null : Number(maxDaysPerRequest),
          genderRestriction: genderRestriction === '' ? null : genderRestriction,
          maxTimesInService: maxTimesInService === '' ? null : Number(maxTimesInService),
          isCashableOnTermination,
          isActive
        }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/leave-types/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!leaveTypeId) return;
    try {
      await remove.mutateAsync(leaveTypeId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/leave-types');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 800 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.attendance.leaveTypes.add') : `${t('hr.attendance.leaveTypes.title')} — ${leaveType?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/leave-types') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.attendance.leaveTypes.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (leaveType?.code ?? '')}
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
            <FieldWrapper label={t('hr.attendance.leaveTypes.accrualMethod')}>
              <SearchableSelect
                value={accrualMethod}
                onChange={(v) => setAccrualMethod(v as LeaveAccrualMethod)}
                options={ACCRUAL_METHODS.map((m) => ({ value: m, label: t(`hr.attendance.leaveTypes.accrualMethods.${m}`) }))}
                style={{ minWidth: 180 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.annualDays')}>
              <Input type="number" value={annualDays} onChange={(e) => setAnnualDays(Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.maxCarryOver')}>
              <Input type="number" value={maxCarryOver} onChange={(e) => setMaxCarryOver(Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.isPaid')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isPaid} onChange={(e) => setIsPaid(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.paidPercentage')}>
              <Input type="number" value={paidPercentage} onChange={(e) => setPaidPercentage(Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.deductFrom')}>
              <SearchableSelect value={deductFromLeaveTypeId} onChange={(v) => setDeductFromLeaveTypeId(v === '' ? '' : Number(v))} options={deductFromOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.requiresDocument')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={requiresDocument} onChange={(e) => setRequiresDocument(e.target.checked)} />
              </label>
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.maxDaysPerRequest')}>
              <Input type="number" value={maxDaysPerRequest} onChange={(e) => setMaxDaysPerRequest(e.target.value === '' ? '' : Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.genderRestriction')}>
              <SearchableSelect value={genderRestriction} onChange={(v) => setGenderRestriction(v === '' ? '' : (v as Gender))} options={genderOptions} style={{ minWidth: 160 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.maxTimesInService')}>
              <Input type="number" value={maxTimesInService} onChange={(e) => setMaxTimesInService(e.target.value === '' ? '' : Number(e.target.value))} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.attendance.leaveTypes.isCashableOnTermination')}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                <input type="checkbox" checked={isCashableOnTermination} onChange={(e) => setIsCashableOnTermination(e.target.checked)} />
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
