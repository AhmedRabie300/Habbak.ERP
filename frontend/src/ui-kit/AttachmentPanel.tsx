import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Card, CardBody } from './Card';
import { Icon } from './Icon';
import { useToastStore } from '../store/toastStore';
import { getFieldErrorMessage } from '../app/api';
import { useAttachmentsList, useDeleteAttachment, useUploadAttachment, downloadAttachment } from '../features/common/attachments/api';

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

interface AttachmentPanelProps {
  entityType: string;
  entityId: number | undefined;
}

/** My Remarks/Remarks2.md, remark 3.3 — the generic "Attachments" panel any Edit/New screen can
 * drop in once its own record has an Id; supports uploading multiple files at once and lists
 * every attachment already on the record with download/delete actions. */
export function AttachmentPanel({ entityType, entityId }: AttachmentPanelProps) {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: attachments } = useAttachmentsList(entityType, entityId);
  const uploadMutation = useUploadAttachment(entityType, entityId);
  const deleteMutation = useDeleteAttachment(entityType, entityId);
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (entityId === undefined) {
    return (
      <Card>
        <CardBody>
          <p style={{ fontSize: 13, color: 'var(--color-text-muted)', margin: 0 }}>{t('attachments.saveFirst')}</p>
        </CardBody>
      </Card>
    );
  }

  const handleFiles = async (files: FileList) => {
    for (const file of Array.from(files)) {
      try {
        await uploadMutation.mutateAsync(file);
      } catch (error) {
        const message = getFieldErrorMessage(error);
        if (message) showToast(message, 'error');
      }
    }
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteMutation.mutateAsync(id);
      showToast(t('attachments.deleteSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card>
      <CardBody>
        <div data-no-print="true" style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
          <input ref={fileInputRef} type="file" multiple onChange={(e) => e.target.files && handleFiles(e.target.files)} />
        </div>

        {(!attachments || attachments.length === 0) && (
          <p style={{ fontSize: 13, color: 'var(--color-text-muted)', margin: 0 }}>{t('attachments.noAttachments')}</p>
        )}

        {attachments && attachments.length > 0 && (
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('attachments.fileName')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('attachments.fileSize')}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{t('attachments.uploadedAt')}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {attachments.map((a) => (
                <tr key={a.id}>
                  <td style={{ padding: 6 }}>{a.fileName}</td>
                  <td style={{ padding: 6, color: 'var(--color-text-muted)' }}>{formatFileSize(a.fileSizeBytes)}</td>
                  <td style={{ padding: 6, color: 'var(--color-text-muted)' }}>{new Date(a.uploadedAtUtc).toLocaleString()}</td>
                  <td style={{ padding: 6, display: 'flex', gap: 10 }}>
                    <span onClick={() => downloadAttachment(a.id, a.fileName)} title={t('common.export')} style={{ cursor: 'pointer', display: 'inline-flex' }}>
                      <Icon name="download" size={14} />
                    </span>
                    <span onClick={() => handleDelete(a.id)} title={t('common.remove')} style={{ cursor: 'pointer', display: 'inline-flex' }}>
                      <Icon name="trash" size={14} />
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </CardBody>
    </Card>
  );
}
