import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useCreateContract } from '../../employees/api';
import type { ContractType } from '../../employees/types';

const CONTRACT_TYPES: ContractType[] = ['FixedTerm', 'Indefinite'];

interface FormState {
  contractType: ContractType;
  startDate: string;
  endDate: string;
  probationEndDate: string;
  basicSalary: string;
  insurableWage: string;
  workingHoursPerDay: string;
}

/** خطوة 3: العقد (شامل الراتب الأساسي) — نفس حقول ContractsTab وقت الإنشاء، القرار الأول (Create
 * دايمًا هنا، الموظف لسه جديد فمفيش عقد سابق يتجدد منه). */
export function ContractStep({ employeeId, onComplete }: { employeeId: number; onComplete: () => void }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);

  const [form, setForm] = useState<FormState>({
    contractType: 'Indefinite', startDate: new Date().toISOString().slice(0, 10), endDate: '',
    probationEndDate: '', basicSalary: '', insurableWage: '', workingHoursPerDay: '8'
  });

  const createContract = useCreateContract(employeeId);

  const handleNext = async () => {
    if (!form.basicSalary || !form.insurableWage) {
      showToast(t('hr.hiring.requiredFields'), 'error');
      return;
    }
    try {
      await createContract.mutateAsync({
        contractType: form.contractType,
        startDate: form.startDate,
        endDate: form.endDate || undefined,
        probationEndDate: form.probationEndDate || undefined,
        basicSalary: Number(form.basicSalary),
        insurableWage: Number(form.insurableWage),
        workingHoursPerDay: Number(form.workingHoursPerDay || 0)
      });
      onComplete();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <FieldWrapper label={t('hr.employees.contracts.type')}>
            <SearchableSelect
              value={form.contractType}
              onChange={(v) => setForm((f) => ({ ...f, contractType: v as ContractType }))}
              options={CONTRACT_TYPES.map((v) => ({ value: v, label: t(`hr.employees.contracts.types.${v}`) }))}
              style={{ minWidth: 160 }}
            />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.startDate')}>
            <Input type="date" value={form.startDate} onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.endDate')}>
            <Input type="date" value={form.endDate} onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.probationEndDate')}>
            <Input type="date" value={form.probationEndDate} onChange={(e) => setForm((f) => ({ ...f, probationEndDate: e.target.value }))} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.basicSalary')}>
            <Input type="number" value={form.basicSalary} onChange={(e) => setForm((f) => ({ ...f, basicSalary: e.target.value }))} style={{ width: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.insurableWage')}>
            <Input type="number" value={form.insurableWage} onChange={(e) => setForm((f) => ({ ...f, insurableWage: e.target.value }))} style={{ width: 140 }} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.contracts.workingHoursPerDay')}>
            <Input type="number" value={form.workingHoursPerDay} onChange={(e) => setForm((f) => ({ ...f, workingHoursPerDay: e.target.value }))} style={{ width: 100 }} />
          </FieldWrapper>
        </div>
        <div style={{ marginTop: 16 }}>
          <Button variant="primary" onClick={handleNext}>{t('hr.hiring.next')}</Button>
        </div>
      </CardBody>
    </Card>
  );
}
