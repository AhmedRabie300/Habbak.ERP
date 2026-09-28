import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../../ui-kit/DataGrid';
import { Button } from '../../../../ui-kit/Button';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../../ui-kit/Badge';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useContracts, useCreateContract, useRenewContract, useTerminateContract } from '../api';
import type { ContractType, EmploymentContract, EmploymentContractInput } from '../types';

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

const emptyForm = (): FormState => ({
  contractType: 'Indefinite', startDate: new Date().toISOString().slice(0, 10), endDate: '',
  probationEndDate: '', basicSalary: '', insurableWage: '', workingHoursPerDay: '8'
});

/** تبويب "عقود" — List + إنشاء/تجديد/إنهاء (Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5,
 * Sub-Batch 1.5.3 — مبني كامل من الأول فمفيش داعي لتكرارها في 1.5.4). */
export function ContractsTab({ employeeId }: { employeeId: number }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const [page, setPage] = useState(1);
  const { data, isLoading } = useContracts(employeeId, { page, pageSize: 25 });

  const [mode, setMode] = useState<'list' | 'create' | 'renew'>('list');
  const [renewingContract, setRenewingContract] = useState<EmploymentContract | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());

  const createContract = useCreateContract(employeeId);
  const renewContract = useRenewContract(employeeId, renewingContract?.id);
  const terminateContract = useTerminateContract(employeeId);

  useEffect(() => {
    if (mode === 'renew' && renewingContract) {
      setForm({
        contractType: renewingContract.contractType, startDate: new Date().toISOString().slice(0, 10), endDate: '',
        probationEndDate: '', basicSalary: String(renewingContract.basicSalary), insurableWage: String(renewingContract.insurableWage),
        workingHoursPerDay: String(renewingContract.workingHoursPerDay)
      });
    }
  }, [mode, renewingContract]);

  const startCreate = () => { setMode('create'); setForm(emptyForm()); };
  const startRenew = (contract: EmploymentContract) => { setRenewingContract(contract); setMode('renew'); };
  const cancel = () => { setMode('list'); setRenewingContract(null); };

  const handleSubmit = async () => {
    const input: EmploymentContractInput = {
      contractType: form.contractType,
      startDate: form.startDate,
      endDate: form.endDate || undefined,
      probationEndDate: form.probationEndDate || undefined,
      basicSalary: Number(form.basicSalary || 0),
      insurableWage: Number(form.insurableWage || 0),
      workingHoursPerDay: Number(form.workingHoursPerDay || 0)
    };
    try {
      if (mode === 'renew') {
        await renewContract.mutateAsync(input);
        showToast(t('hr.employees.contracts.renewSuccess'), 'success');
      } else {
        await createContract.mutateAsync(input);
        showToast(t('hr.employees.contracts.createSuccess'), 'success');
      }
      cancel();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleTerminate = async (contract: EmploymentContract) => {
    try {
      await terminateContract.mutateAsync(contract.id);
      showToast(t('hr.employees.contracts.terminateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<EmploymentContract>[] = [
    { key: 'contractType', label: t('hr.employees.contracts.type'), render: (r) => t(`hr.employees.contracts.types.${r.contractType}`), exportValue: (r) => r.contractType },
    { key: 'startDate', label: t('hr.employees.contracts.startDate'), render: (r) => r.startDate, exportValue: (r) => r.startDate },
    { key: 'endDate', label: t('hr.employees.contracts.endDate'), render: (r) => r.endDate ?? '—', exportValue: (r) => r.endDate ?? '' },
    { key: 'basicSalary', label: t('hr.employees.contracts.basicSalary'), render: (r) => r.basicSalary.toLocaleString(), exportValue: (r) => r.basicSalary },
    { key: 'status', label: t('hr.employees.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status },
    {
      key: 'actions',
      label: t('hr.employees.contracts.actions'),
      render: (r) =>
        r.status === 'Active' ? (
          <div style={{ display: 'flex', gap: 6 }}>
            <Button variant="secondary" size="sm" onClick={() => startRenew(r)}>{t('hr.employees.contracts.renew')}</Button>
            <Button variant="secondary" size="sm" onClick={() => handleTerminate(r)}>{t('hr.employees.contracts.terminateAction')}</Button>
          </div>
        ) : (
          '—'
        )
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {mode === 'list' && (
        <>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Button variant="primary" onClick={startCreate}>{t('hr.employees.contracts.add')}</Button>
          </div>
          <DataGrid columns={columns} data={data} isLoading={isLoading} search="" onSearchChange={() => undefined} page={page} onPageChange={setPage} exportFileName={t('hr.employees.contracts.title')} />
        </>
      )}

      {mode !== 'list' && (
        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{mode === 'renew' ? t('hr.employees.contracts.renew') : t('hr.employees.contracts.add')}</h3>
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
            <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
              <Button variant="primary" onClick={handleSubmit}>{t('common.save')}</Button>
              <Button variant="secondary" onClick={cancel}>{t('common.cancel')}</Button>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
