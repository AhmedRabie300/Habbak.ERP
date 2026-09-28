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
import { useDeleteJobGrade, useJobGrades, useSaveJobGrade, type JobGrade } from './api';
import { toPaged } from '../listing';

/** /hr/job-grades — screen HR_JOB_GRADES. */
export function JobGradesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data, isLoading } = useJobGrades();

  const rows = toPaged(data, search, (g, q) => `${g.code} ${g.nameAr} ${g.nameEn}`.toLowerCase().includes(q));

  const columns: DataGridColumn<JobGrade>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'level', label: t('hr.jobGrades.level'), render: (r) => r.level, exportValue: (r) => String(r.level) },
    { key: 'minSalary', label: t('hr.jobGrades.minSalary'), render: (r) => r.minSalary ?? '—', exportValue: (r) => String(r.minSalary ?? '') },
    { key: 'maxSalary', label: t('hr.jobGrades.maxSalary'), render: (r) => r.maxSalary ?? '—', exportValue: (r) => String(r.maxSalary ?? '') },
    { key: 'isActive', label: t('hr.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.jobGrades.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/hr/job-grades/new')}>
            {t('hr.jobGrades.add')}
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
        onRowClick={(row) => navigate(`/hr/job-grades/${row.id}`)}
        exportFileName={t('hr.jobGrades.title')}
      />
    </div>
  );
}

/** /hr/job-grades/:id */
export function JobGradeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const gradeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: grades, isLoading } = useJobGrades();
  const grade = grades?.find((g) => g.id === gradeId);
  const { data: codingRule } = useCodingRule('HR_JOB_GRADES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [level, setLevel] = useState('');
  const [minSalary, setMinSalary] = useState('');
  const [maxSalary, setMaxSalary] = useState('');
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    if (!grade) return;
    setNameAr(grade.nameAr);
    setNameEn(grade.nameEn);
    setLevel(String(grade.level));
    setMinSalary(grade.minSalary?.toString() ?? '');
    setMaxSalary(grade.maxSalary?.toString() ?? '');
    setIsActive(grade.isActive);
  }, [grade]);

  const save = useSaveJobGrade(gradeId);
  const remove = useDeleteJobGrade();

  const handleSave = async () => {
    try {
      const result = await save.mutateAsync({
        code: codeIsAutomatic || !isNew ? undefined : code,
        data: {
          nameAr,
          nameEn,
          level: Number(level || 0),
          minSalary: minSalary === '' ? null : Number(minSalary),
          maxSalary: maxSalary === '' ? null : Number(maxSalary),
          isActive
        }
      });
      showToast(t('hr.saveSuccess'), 'success');
      if (isNew) navigate(`/hr/job-grades/${result.id}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async () => {
    if (!gradeId) return;
    try {
      await remove.mutateAsync(gradeId);
      showToast(t('hr.deleteSuccess'), 'success');
      navigate('/hr/job-grades');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 700 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('hr.jobGrades.add') : `${t('hr.jobGrades.title')} — ${grade?.code ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/hr/job-grades') }]}
        destructive={!isNew ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('hr.jobGrades.deleteConfirm') }] : []}
      />

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : code) : (grade?.code ?? '')}
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
            <FieldWrapper label={t('hr.jobGrades.level')}>
              <Input type="number" value={level} onChange={(e) => setLevel(e.target.value)} style={{ width: 100 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.jobGrades.minSalary')}>
              <Input type="number" value={minSalary} onChange={(e) => setMinSalary(e.target.value)} style={{ width: 140 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.jobGrades.maxSalary')}>
              <Input type="number" value={maxSalary} onChange={(e) => setMaxSalary(e.target.value)} style={{ width: 140 }} />
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
