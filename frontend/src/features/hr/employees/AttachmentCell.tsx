import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';

/** Docs/Implementation/Phase-3C-Research.md §3.3 — upload-then-link on an existing row (no Update UI
 * for contracts/certifications yet), same file-picker pattern as DocumentsTab's create form but as a
 * per-row grid action instead. */
export function AttachmentCell({
  attachmentId,
  onUpload,
  onDownload
}: {
  attachmentId: number | null;
  onUpload: (file: File) => void;
  onDownload: () => void;
}) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <div style={{ display: 'flex', gap: 6 }}>
      <input
        ref={inputRef}
        type="file"
        style={{ display: 'none' }}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onUpload(file);
          if (inputRef.current) inputRef.current.value = '';
        }}
      />
      {attachmentId !== null && (
        <Button variant="secondary" size="sm" onClick={onDownload}>{t('common.export')}</Button>
      )}
      <Button variant="secondary" size="sm" onClick={() => inputRef.current?.click()}>
        {attachmentId !== null ? t('hr.employees.attachment.replace') : t('hr.employees.attachment.upload')}
      </Button>
    </div>
  );
}
