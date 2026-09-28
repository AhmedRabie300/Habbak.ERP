import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useCertifications, useCreateCertification, useDeleteCertification } from '../api';
import type { EmployeeCertification } from '../types';

interface FormState {
  nameAr: string;
  nameEn: string;
  issuer: string;
  issueDate: string;
  expiryDate: string;
  certificateNumber: string;
}

const emptyForm = (): FormState => ({ nameAr: '', nameEn: '', issuer: '', issueDate: new Date().toISOString().slice(0, 10), expiryDate: '', certificateNumber: '' });

/** تبويب "شهادات" — قائمة + إضافة (Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch
 * 1.5.3 — مبني كامل، مفيش تعديل بعد الإنشاء في هذه المرحلة (حذف وإعادة إضافة بدل Update، الكيان
 * بسيط ومفيهوش Status يتغيّر زي العقود). */
export function CertificationsTab({ employeeId }: { employeeId: number }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: certifications, isLoading } = useCertifications(employeeId);

  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm());

  const createCertification = useCreateCertification(employeeId);
  const deleteCertification = useDeleteCertification(employeeId);

  const startCreate = () => { setCreating(true); setForm(emptyForm()); };
  const cancel = () => setCreating(false);

  const handleSubmit = async () => {
    if (!form.nameAr || !form.nameEn || !form.issuer) {
      showToast(t('hr.employees.certifications.requiredFields'), 'error');
      return;
    }
    try {
      await createCertification.mutateAsync({
        nameAr: form.nameAr, nameEn: form.nameEn, issuer: form.issuer, issueDate: form.issueDate,
        expiryDate: form.expiryDate || undefined, certificateNumber: form.certificateNumber || undefined
      });
      showToast(t('hr.saveSuccess'), 'success');
      cancel();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async (certification: EmployeeCertification) => {
    try {
      await deleteCertification.mutateAsync(certification.id);
      showToast(t('hr.deleteSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        {!creating && <Button variant="primary" onClick={startCreate}>{t('hr.employees.certifications.add')}</Button>}
      </div>

      {creating && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={t('hr.nameAr')}>
                <Input value={form.nameAr} onChange={(e) => setForm((f) => ({ ...f, nameAr: e.target.value }))} style={{ minWidth: 200 }} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.nameEn')}>
                <Input value={form.nameEn} onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))} style={{ minWidth: 200 }} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.certifications.issuer')}>
                <Input value={form.issuer} onChange={(e) => setForm((f) => ({ ...f, issuer: e.target.value }))} style={{ minWidth: 180 }} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.issueDate')}>
                <Input type="date" value={form.issueDate} onChange={(e) => setForm((f) => ({ ...f, issueDate: e.target.value }))} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.expiryDate')}>
                <Input type="date" value={form.expiryDate} onChange={(e) => setForm((f) => ({ ...f, expiryDate: e.target.value }))} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.certifications.certificateNumber')}>
                <Input value={form.certificateNumber} onChange={(e) => setForm((f) => ({ ...f, certificateNumber: e.target.value }))} style={{ width: 160 }} />
              </FieldWrapper>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
              <Button variant="primary" onClick={handleSubmit}>{t('common.save')}</Button>
              <Button variant="secondary" onClick={cancel}>{t('common.cancel')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      {(!certifications || certifications.length === 0) && !creating && (
        <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
      )}

      {certifications && certifications.length > 0 && (
        <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.nameAr')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.certifications.issuer')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.documents.issueDate')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.documents.expiryDate')}</th>
              <th style={{ textAlign: 'start', padding: 6 }} />
            </tr>
          </thead>
          <tbody>
            {certifications.map((c) => (
              <tr key={c.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={{ padding: 6 }}>{c.nameAr}</td>
                <td style={{ padding: 6 }}>{c.issuer}</td>
                <td style={{ padding: 6 }}>{c.issueDate}</td>
                <td style={{ padding: 6 }}>{c.expiryDate ?? '—'}</td>
                <td style={{ padding: 6 }}>
                  <Button variant="secondary" size="sm" onClick={() => handleDelete(c)}>{t('common.remove')}</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
