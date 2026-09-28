import { useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { SearchableSelect } from '../../../../ui-kit/SearchableSelect';
import { Button } from '../../../../ui-kit/Button';
import { downloadAttachment } from '../../../common/attachments/api';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useDocumentTypes } from '../../documentTypes/api';
import { useCreateDocument, useDeleteDocument, useDocuments, useUploadDocumentFile } from '../api';
import type { EmployeeDocument } from '../types';

interface FormState {
  employeeDocumentTypeId: number | '';
  issueDate: string;
  expiryDate: string;
  documentNumber: string;
  file: File | null;
}

const emptyForm = (): FormState => ({ employeeDocumentTypeId: '', issueDate: new Date().toISOString().slice(0, 10), expiryDate: '', documentNumber: '', file: null });

/** تبويب "مستندات" — EmployeeDocument محتاج AttachmentId قبل الإنشاء (مفيش رفع منفصل جوه
 * Endpoint المستند نفسه) — نرفع الملف عن طريق `/attachments` العام الأول (نفس الآلية اللي
 * `AttachmentPanel` بيستخدمها)، وبعدين نمرر الـ id الراجع كـ attachmentId (Docs/Implementation/
 * HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.3 — مبني كامل من الأول). */
export function DocumentsTab({ employeeId }: { employeeId: number }) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: documents, isLoading } = useDocuments(employeeId);
  const { data: documentTypes } = useDocumentTypes();
  const typeName = (id: number) => documentTypes?.find((d) => d.id === id)?.nameAr ?? '—';
  const typeRequiresExpiry = (id: number | '') => (id === '' ? false : (documentTypes?.find((d) => d.id === id)?.requiresExpiry ?? false));

  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState<FormState>(emptyForm());
  const fileInputRef = useRef<HTMLInputElement>(null);

  const typeOptions = useMemo(() => (documentTypes ?? []).map((d) => ({ value: d.id, label: d.nameAr })), [documentTypes]);

  const uploadFile = useUploadDocumentFile(employeeId);
  const createDocument = useCreateDocument(employeeId);
  const deleteDocument = useDeleteDocument(employeeId);

  const startCreate = () => { setCreating(true); setForm(emptyForm()); };
  const cancel = () => { setCreating(false); if (fileInputRef.current) fileInputRef.current.value = ''; };

  const handleSubmit = async () => {
    if (form.employeeDocumentTypeId === '' || !form.file) {
      showToast(t('hr.employees.documents.fileAndTypeRequired'), 'error');
      return;
    }
    if (typeRequiresExpiry(form.employeeDocumentTypeId) && !form.expiryDate) {
      showToast(t('hr.employees.documents.expiryRequired'), 'error');
      return;
    }
    try {
      const uploaded = await uploadFile.mutateAsync(form.file);
      await createDocument.mutateAsync({
        employeeDocumentTypeId: Number(form.employeeDocumentTypeId),
        issueDate: form.issueDate,
        expiryDate: form.expiryDate || undefined,
        attachmentId: uploaded.id,
        documentNumber: form.documentNumber || undefined
      });
      showToast(t('hr.saveSuccess'), 'success');
      cancel();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async (doc: EmployeeDocument) => {
    try {
      await deleteDocument.mutateAsync(doc.id);
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
        {!creating && <Button variant="primary" onClick={startCreate}>{t('hr.employees.documents.add')}</Button>}
      </div>

      {creating && (
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('hr.documentTypes.title')}>
                <SearchableSelect value={form.employeeDocumentTypeId} onChange={(v) => setForm((f) => ({ ...f, employeeDocumentTypeId: v === '' ? '' : Number(v) }))} options={typeOptions} style={{ minWidth: 200 }} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.issueDate')}>
                <Input type="date" value={form.issueDate} onChange={(e) => setForm((f) => ({ ...f, issueDate: e.target.value }))} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.expiryDate')}>
                <Input type="date" value={form.expiryDate} onChange={(e) => setForm((f) => ({ ...f, expiryDate: e.target.value }))} disabled={!typeRequiresExpiry(form.employeeDocumentTypeId)} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.documentNumber')}>
                <Input value={form.documentNumber} onChange={(e) => setForm((f) => ({ ...f, documentNumber: e.target.value }))} style={{ width: 160 }} />
              </FieldWrapper>
              <FieldWrapper label={t('hr.employees.documents.file')}>
                <input ref={fileInputRef} type="file" onChange={(e) => setForm((f) => ({ ...f, file: e.target.files?.[0] ?? null }))} />
              </FieldWrapper>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
              <Button variant="primary" onClick={handleSubmit}>{t('common.save')}</Button>
              <Button variant="secondary" onClick={cancel}>{t('common.cancel')}</Button>
            </div>
          </CardBody>
        </Card>
      )}

      {(!documents || documents.length === 0) && !creating && (
        <p style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>{t('common.noData')}</p>
      )}

      {documents && documents.length > 0 && (
        <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.documentTypes.title')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.documents.issueDate')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.documents.expiryDate')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('hr.employees.documents.documentNumber')}</th>
              <th style={{ textAlign: 'start', padding: 6 }} />
            </tr>
          </thead>
          <tbody>
            {documents.map((doc) => (
              <tr key={doc.id} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={{ padding: 6 }}>{typeName(doc.employeeDocumentTypeId)}</td>
                <td style={{ padding: 6 }}>{doc.issueDate}</td>
                <td style={{ padding: 6 }}>{doc.expiryDate ?? '—'}</td>
                <td style={{ padding: 6 }}>{doc.documentNumber ?? '—'}</td>
                <td style={{ padding: 6, display: 'flex', gap: 6 }}>
                  <Button variant="secondary" size="sm" onClick={() => downloadAttachment(doc.attachmentId, `document-${doc.id}`)}>{t('common.export')}</Button>
                  <Button variant="secondary" size="sm" onClick={() => handleDelete(doc)}>{t('common.remove')}</Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
