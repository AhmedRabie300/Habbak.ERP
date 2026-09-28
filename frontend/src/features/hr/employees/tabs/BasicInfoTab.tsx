import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useCodingRule } from '../../../settings/codingRules/api';
import { useBranchesList } from '../../../organization/branches/api';
import { useUsersList } from '../../../settings/security/api';
import { useOrgUnits } from '../../orgUnits/api';
import { useJobPositions } from '../../jobPositions/api';
import { useJobGrades } from '../../jobGrades/api';
import { useAssignUserToEmployee, useCreateEmployee, useEmployeesLookup, useUpdateEmployee } from '../api';
import type { EmployeeDetail, EmploymentType } from '../types';

const EMPLOYMENT_TYPES: EmploymentType[] = ['FullTime', 'PartTime', 'Temporary'];

interface FormState {
  code: string;
  nameAr: string;
  nameEn: string;
  branchId: number | '';
  orgUnitId: number | '';
  jobPositionId: number | '';
  jobGradeId: number | '';
  managerId: number | '';
  hireDate: string;
  employmentType: EmploymentType;
  isActive: boolean;
}

const emptyForm = (): FormState => ({
  code: '', nameAr: '', nameEn: '', branchId: '', orgUnitId: '', jobPositionId: '', jobGradeId: '',
  managerId: '', hireDate: new Date().toISOString().slice(0, 10), employmentType: 'FullTime', isActive: true
});

interface BasicInfoTabProps {
  employeeId: number | undefined;
  employee: EmployeeDetail | null;
  isNew: boolean;
}

/** تبويب "بيانات" — الحقول الأساسية للموظف + ربط مستخدم (Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.3). */
export function BasicInfoTab({ employeeId, employee, isNew }: BasicInfoTabProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: codingRule } = useCodingRule('HR_EMPLOYEES');
  const codeIsAutomatic = codingRule?.isAutomatic ?? false;

  const { data: branches } = useBranchesList();
  const branchOptions = useMemo(() => (branches ?? []).map((b) => ({ value: b.id, label: b.nameAr })), [branches]);

  const { data: orgUnits } = useOrgUnits();
  const orgUnitOptions = useMemo(() => (orgUnits ?? []).map((u) => ({ value: u.id, label: u.nameAr })), [orgUnits]);

  const { data: jobPositions } = useJobPositions();
  const jobPositionOptions = useMemo(() => (jobPositions ?? []).map((p) => ({ value: p.id, label: p.nameAr })), [jobPositions]);

  const { data: jobGrades } = useJobGrades();
  const jobGradeOptions = useMemo(() => (jobGrades ?? []).map((g) => ({ value: g.id, label: g.nameAr })), [jobGrades]);

  const { data: employeesLookup } = useEmployeesLookup();
  const managerOptions = useMemo(
    () => [
      { value: '', label: t('hr.employees.noManager') },
      ...(employeesLookup ?? []).filter((e) => e.id !== employeeId).map((e) => ({ value: e.id, label: `${e.code} — ${e.nameAr}` }))
    ],
    [employeesLookup, employeeId, t]
  );

  const [form, setForm] = useState<FormState>(emptyForm());

  useEffect(() => {
    if (!employee) return;
    setForm({
      code: employee.code, nameAr: employee.nameAr, nameEn: employee.nameEn,
      branchId: employee.branchId ?? '', orgUnitId: employee.orgUnitId, jobPositionId: employee.jobPositionId,
      jobGradeId: employee.jobGradeId, managerId: employee.managerId ?? '', hireDate: employee.hireDate,
      employmentType: employee.employmentType, isActive: employee.isActive
    });
  }, [employee]);

  const createEmployee = useCreateEmployee();
  const updateEmployee = useUpdateEmployee(employeeId);

  const handleSave = async () => {
    try {
      if (isNew) {
        const result = await createEmployee.mutateAsync({
          code: codeIsAutomatic ? undefined : form.code,
          nameAr: form.nameAr,
          nameEn: form.nameEn,
          branchId: Number(form.branchId),
          orgUnitId: Number(form.orgUnitId),
          jobPositionId: Number(form.jobPositionId),
          jobGradeId: Number(form.jobGradeId),
          managerId: form.managerId === '' ? undefined : Number(form.managerId),
          hireDate: form.hireDate,
          employmentType: form.employmentType
        });
        showToast(t('hr.saveSuccess'), 'success');
        navigate(`/hr/employees/${result.id}`);
      } else {
        await updateEmployee.mutateAsync({
          nameAr: form.nameAr,
          nameEn: form.nameEn,
          branchId: Number(form.branchId),
          orgUnitId: Number(form.orgUnitId),
          jobPositionId: Number(form.jobPositionId),
          jobGradeId: Number(form.jobGradeId),
          managerId: form.managerId === '' ? undefined : Number(form.managerId),
          hireDate: form.hireDate,
          employmentType: form.employmentType,
          isActive: form.isActive
        });
        showToast(t('hr.saveSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 900 }}>
      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('hr.code')}>
              <Input
                value={isNew ? (codeIsAutomatic ? t('codingRules.autoGeneratedPlaceholder') : form.code) : form.code}
                onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                disabled={!isNew || codeIsAutomatic}
              />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameAr')}>
              <Input value={form.nameAr} onChange={(e) => setForm((f) => ({ ...f, nameAr: e.target.value }))} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.nameEn')}>
              <Input value={form.nameEn} onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.orgUnits.branch')}>
              <SearchableSelect value={form.branchId} onChange={(v) => setForm((f) => ({ ...f, branchId: v === '' ? '' : Number(v) }))} options={branchOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.orgUnits.title')}>
              <SearchableSelect value={form.orgUnitId} onChange={(v) => setForm((f) => ({ ...f, orgUnitId: v === '' ? '' : Number(v) }))} options={orgUnitOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.jobPositions.title')}>
              <SearchableSelect value={form.jobPositionId} onChange={(v) => setForm((f) => ({ ...f, jobPositionId: v === '' ? '' : Number(v) }))} options={jobPositionOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.jobGrades.title')}>
              <SearchableSelect value={form.jobGradeId} onChange={(v) => setForm((f) => ({ ...f, jobGradeId: v === '' ? '' : Number(v) }))} options={jobGradeOptions} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.employees.manager')}>
              <SearchableSelect value={form.managerId} onChange={(v) => setForm((f) => ({ ...f, managerId: v === '' ? '' : Number(v) }))} options={managerOptions} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.employees.hireDate')}>
              <Input type="date" value={form.hireDate} onChange={(e) => setForm((f) => ({ ...f, hireDate: e.target.value }))} />
            </FieldWrapper>
            <FieldWrapper label={t('hr.employees.employmentType')}>
              <SearchableSelect
                value={form.employmentType}
                onChange={(v) => setForm((f) => ({ ...f, employmentType: v as EmploymentType }))}
                options={EMPLOYMENT_TYPES.map((v) => ({ value: v, label: t(`hr.employees.employmentTypes.${v}`) }))}
                style={{ minWidth: 160 }}
              />
            </FieldWrapper>
            {!isNew && (
              <FieldWrapper label={t('hr.isActive')}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 38 }}>
                  <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
                </label>
              </FieldWrapper>
            )}
          </div>
          <div style={{ marginTop: 16 }}>
            <Button variant="primary" onClick={handleSave}>{t('common.saveChanges')}</Button>
          </div>
        </CardBody>
      </Card>

      {!isNew && employeeId && <AssignUserCard employeeId={employeeId} currentUserId={employee?.userId ?? null} />}
    </div>
  );
}

function AssignUserCard({ employeeId, currentUserId }: { employeeId: number; currentUserId: number | null }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: users } = useUsersList('', '');
  const [userId, setUserId] = useState<number | ''>(currentUserId ?? '');
  const assignUser = useAssignUserToEmployee(employeeId);

  useEffect(() => setUserId(currentUserId ?? ''), [currentUserId]);

  const userOptions = useMemo(() => (users ?? []).map((u) => ({ value: u.id, label: `${u.username} — ${u.fullName}` })), [users]);

  const handleAssign = async () => {
    if (userId === '') return;
    try {
      await assignUser.mutateAsync(Number(userId));
      showToast(t('hr.employees.assignUserSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <FieldWrapper label={t('hr.employees.linkedUser')}>
            <SearchableSelect value={userId} onChange={(v) => setUserId(v === '' ? '' : Number(v))} options={userOptions} style={{ minWidth: 260 }} />
          </FieldWrapper>
          <Button variant="secondary" onClick={handleAssign}>{t('hr.employees.assignUser')}</Button>
        </div>
      </CardBody>
    </Card>
  );
}
