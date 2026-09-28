import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from '../../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../../ui-kit/Field';
import { Button } from '../../../../ui-kit/Button';
import { useToastStore } from '../../../../store/toastStore';
import { getFieldErrorMessage } from '../../../../app/api';
import { useDocumentTypes } from '../../documentTypes/api';
import { useCreateDocument, useUploadDocumentFile } from '../../employees/api';
import type { DocumentType } from '../../documentTypes/api';

/**
 * خطوة 4: المستندات الإلزامية — بس أنواع المستندات اللي `IsMandatory = true` (Docs/Implementation/
 * HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.5). مستندات إضافية غير إلزامية تتضاف بعدين من تبويب
 * "مستندات" في شاشة الموظف الكاملة (1.5.3) — مش من هنا. لو مفيش أنواع إلزامية معرّفة في النظام،
 * الخطوة تتخطى تلقائيًا (مفيش حاجة تمنع الاستمرار).
 */
export function DocumentsStep({ employeeId, onComplete }: { employeeId: number; onComplete: () => void }) {
  const { t } = useTranslation();
  const { data: documentTypes, isLoading } = useDocumentTypes();
  const mandatoryTypes = (documentTypes ?? []).filter((d) => d.isMandatory);
  const [uploadedIds, setUploadedIds] = useState<Set<number>>(new Set());

  const allUploaded = mandatoryTypes.every((d) => uploadedIds.has(d.id));

  if (isLoading) return <div>{t('common.loading')}</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {mandatoryTypes.length === 0 && (
        <Card>
          <CardBody>
            <p style={{ fontSize: 13, color: 'var(--color-text-muted)', margin: 0 }}>{t('hr.hiring.noMandatoryDocuments')}</p>
          </CardBody>
        </Card>
      )}

      {mandatoryTypes.map((type) => (
        <MandatoryDocumentRow
          key={type.id}
          employeeId={employeeId}
          type={type}
          uploaded={uploadedIds.has(type.id)}
          onUploaded={() => setUploadedIds((prev) => new Set(prev).add(type.id))}
        />
      ))}

      <div>
        <Button variant="primary" onClick={onComplete} disabled={!allUploaded}>{t('hr.hiring.next')}</Button>
      </div>
    </div>
  );
}

function MandatoryDocumentRow({
  employeeId, type, uploaded, onUploaded
}: {
  employeeId: number;
  type: DocumentType;
  uploaded: boolean;
  onUploaded: () => void;
}) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const [issueDate, setIssueDate] = useState(new Date().toISOString().slice(0, 10));
  const [expiryDate, setExpiryDate] = useState('');
  const [file, setFile] = useState<File | null>(null);

  const uploadFile = useUploadDocumentFile(employeeId);
  const createDocument = useCreateDocument(employeeId);

  const handleUpload = async () => {
    if (!file) {
      showToast(t('hr.employees.documents.fileAndTypeRequired'), 'error');
      return;
    }
    if (type.requiresExpiry && !expiryDate) {
      showToast(t('hr.employees.documents.expiryRequired'), 'error');
      return;
    }
    try {
      const uploadedFile = await uploadFile.mutateAsync(file);
      await createDocument.mutateAsync({
        employeeDocumentTypeId: type.id,
        issueDate,
        expiryDate: expiryDate || undefined,
        attachmentId: uploadedFile.id
      });
      onUploaded();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div style={{ minWidth: 160, fontWeight: 700, fontSize: 13 }}>
            {type.nameAr}
            {uploaded && <span style={{ color: 'var(--color-success)', marginInlineStart: 6 }}>✓</span>}
          </div>
          <FieldWrapper label={t('hr.employees.documents.issueDate')}>
            <Input type="date" value={issueDate} onChange={(e) => setIssueDate(e.target.value)} disabled={uploaded} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.documents.expiryDate')}>
            <Input type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} disabled={!type.requiresExpiry || uploaded} />
          </FieldWrapper>
          <FieldWrapper label={t('hr.employees.documents.file')}>
            <input type="file" disabled={uploaded} onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </FieldWrapper>
          {!uploaded && <Button variant="secondary" size="sm" onClick={handleUpload}>{t('hr.hiring.upload')}</Button>}
        </div>
      </CardBody>
    </Card>
  );
}
