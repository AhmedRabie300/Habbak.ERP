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
import { downloadAttachment } from '../../../common/attachments/api';
import {
  useContractById, useContracts, useCreateContract, useRenewContract, useSetContractAttachment,
  useTerminateContract, useUploadContractFile
} from '../api';
import { AttachmentCell } from '../AttachmentCell';
import { ContractLinesEditor } from '../ContractLinesEditor';
import type { ContractLineInput, ContractType, EmploymentContract, EmploymentContractInput } from '../types';

const CONTRACT_TYPES: ContractType[] = ['FixedTerm', 'Indefinite'];

interface FormState {
  contractType: ContractType;
  startDate: string;
  endDate: string;
  probationEndDate: string;
  basicSalary: string;
  insurableWage: string;
  workingHoursPerDay: string;
  lines: ContractLineInput[];
}

const emptyForm = (): FormState => ({
  contractType: 'Indefinite', startDate: new Date().toISOString().slice(0, 10), endDate: '',
  probationEndDate: '', basicSalary: '', insurableWage: '', workingHoursPerDay: '8', lines: []
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
  const uploadContractFile = useUploadContractFile(employeeId);
  const setContractAttachment = useSetContractAttachment(employeeId);

  // GetList leaves Lines empty (§3.1) — the renewal's copy-forward prefill needs the full detail.
  const { data: renewingContractDetail } = useContractById(employeeId, mode === 'renew' ? renewingContract?.id : undefined);

  useEffect(() => {
    if (mode === 'renew' && renewingContract) {
      setForm({
        contractType: renewingContract.contractType, startDate: new Date().toISOString().slice(0, 10), endDate: '',
        probationEndDate: '', basicSalary: String(renewingContract.basicSalary), insurableWage: String(renewingContract.insurableWage),
        workingHoursPerDay: String(renewingContract.workingHoursPerDay), lines: []
      });
    }
  }, [mode, renewingContract]);

  useEffect(() => {
    if (mode === 'renew' && renewingContractDetail) {
      setForm((f) => ({
        ...f,
        lines: renewingContractDetail.lines.map(({ nameAr, nameEn, amount, type, isTaxable, isInsurable, order }) => ({
          nameAr, nameEn, amount, type, isTaxable, isInsurable, order
        }))
      }));
    }
  }, [mode, renewingContractDetail]);

  const handleUploadAttachment = async (contract: EmploymentContract, file: File) => {
    try {
      const uploaded = await uploadContractFile.mutateAsync(file);
      await setContractAttachment.mutateAsync({ id: contract.id, attachmentId: uploaded.id });
      showToast(t('hr.employees.attachment.success'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

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
      workingHoursPerDay: Number(form.workingHoursPerDay || 0),
      lines: form.lines
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
      key: 'attachment',
      label: t('hr.employees.attachment.title'),
      render: (r) => (
        <AttachmentCell
          attachmentId={r.attachmentId}
          onUpload={(file) => handleUploadAttachment(r, file)}
          onDownload={() => downloadAttachment(r.attachmentId!, `contract-${r.id}`)}
        />
      )
    },
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
            <div style={{ marginTop: 16 }}>
              <ContractLinesEditor lines={form.lines} onChange={(lines) => setForm((f) => ({ ...f, lines }))} />
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
