import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './Button';

interface ConfirmModalProps {
  open: boolean;
  title: string;
  message?: ReactNode;
  onConfirm: () => void;
  onCancel: () => void;
  confirmLabel?: string;
}

/**
 * The one standard confirmation dialog for destructive actions (00-System-Wide-Corrections-01.md,
 * section 1.1: every destructive ActionBar button requires this before it runs). Overlay/header/
 * body/footer structure ported from the NozomSoft reference project's `.overlay`/`.modal` classes.
 */
export function ConfirmModal({ open, title, message, onConfirm, onCancel, confirmLabel }: ConfirmModalProps) {
  const { t } = useTranslation();
  if (!open) {
    return null;
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(15,23,42,0.5)',
        backdropFilter: 'blur(2px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000
      }}
      onClick={onCancel}
    >
      <div
        style={{
          background: 'var(--color-surface)',
          borderRadius: 'var(--radius-lg)',
          boxShadow: 'var(--shadow-lg)',
          width: '90%',
          maxWidth: 420
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
          {title}
        </div>
        {message && (
          <div style={{ padding: '20px 24px', fontSize: 13, color: 'var(--color-text-muted)' }}>{message}</div>
        )}
        <div
          style={{
            padding: '16px 24px',
            borderTop: '1px solid var(--color-border)',
            display: 'flex',
            justifyContent: 'flex-end',
            gap: 10
          }}
        >
          <Button variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
          <Button variant="danger" onClick={onConfirm}>{confirmLabel ?? t('common.delete')}</Button>
        </div>
      </div>
    </div>
  );
}
