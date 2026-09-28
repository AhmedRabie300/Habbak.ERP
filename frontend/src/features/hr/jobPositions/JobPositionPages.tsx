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
import { useOrgUnits } from '../orgUnits/api';
import { useJobGrades } from '../jobGrades/api';
import { toPaged } from '../listing';
import { useDeleteJobPosition, useJobPositions, useSaveJobPosition, type JobPosition } from './api';

/** /hr/job-positions — screen HR_JOB_POSITIONS. */
export function JobPositionsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useJobPositions();
  const { data: orgUnits } = useOrgUnits();
  const orgUnitName = (id: number | null) => orgUnits?.find((u) => u.id === id)?.nameAr ?? '—';

  const rows = toPaged(data, search, (p, q) => `${p.code} ${p.nameAr} ${p.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<JobPosition>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'orgUnit', label: t('hr.orgUnits.title'), render: (r) => orgUnitName(r.orgUnitId), exportValue: (r) => orgUnitName(r.orgUnitId) },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.jobPositions.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/hr/job-positions/new')}>
            {t('hr.jobPositions.add')}
          </Button>
        )}
      </div>

      <DataGrid
        columns={columns}
        data={rows}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => undefined}
        onRowClick={(row) => navigate(`/hr/job-positions/${row.id}`)}
        exportFileName={t('hr.jobPositions.title')}
      />
    </div>
  );
}

/** /hr/job-positions/:id */
export function JobPositionEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const positionId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: positions, isLoading } = useJobPositions();
  const position = positions?.find((p) => p.id === positionId);
  const { data: codingRule } = useCodingRule('HR_JOB_POSITIONS');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const { data: orgUnits } = useOrgUnits();
  const orgUnitOptions = useMemo(
    () => [{ value: '', label: t('hr.jobPositions.noOrgUnit') }, ...(orgUnits ?? []).map((u) => ({ value: u.id, label: u.nameAr }))],
    [orgUnits, t]
  );
  const { data: jobGrades } = useJobGrades();
  const jobGradeOptions = useMemo(
    () => [{ value: '', label: t('hr.jobPositions.noDefaultGrade') }, ...(jobGrades ?? []).map((g) => ({ value: g.id, label: g.nameAr }))],
    [jobGrades, t]
  );

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [orgUnitId, setOrgUnitId] = useState<number | ''>('');
  const [defaultJobGradeId, setDefaultJobGradeId] = useState<number | ''>('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!position) return;
    setNameAr(position.nameAr);
    setNameEn(position.nameEn);
    setOrgUnitId(position.orgUnitId ?? '');
    setDefaultJobGradeId(position.defaultJobGradeId ?? '');
    setIsActive(position.isActive);
  }, [position]);

  const save = useSaveJobPosition(positionId);
  const remove = useDeleteJobPosition();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr,
          nameEn,
          orgUnitId: orgUnitId === '' ? null : Number(orgUnitId),
          defaultJobGradeId: defaultJobGradeId === '' ? null : Number(defaultJobGradeId),
          isActive
        }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/job-positions/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!positionId) return;
    try {
      await remove.mutateAsync(positionId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/job-positions');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.jobPositions.add') : `${t('hr.jobPositions.title')} — ${position?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/job-positions') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.jobPositions.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (position?.code ?? '')}
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
            <FieldWrapper label={t('hr.orgUnits.title')}>
              <SearchableSelect
                value={orgUnitId}
                onChange={(v) => setOrgUnitId(v === '' ? '' : Number(v))}
                options={orgUnitOptions}
                style={{ minWidth: 220 }}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.jobPositions.defaultGrade')}>
              <SearchableSelect
                value={defaultJobGradeId}
                onChange={(v) => setDefaultJobGradeId(v === '' ? '' : Number(v))}
                options={jobGradeOptions}
                style={{ minWidth: 220 }}
              />
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
