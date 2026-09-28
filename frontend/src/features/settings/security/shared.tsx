import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Badge } from '../../../ui-kit/Badge';
import type { UserStatus } from './api';

export function formatDateTime(value: string | null | undefined, language: string): string {
  if (!value) return '—';
  const date = new Date(value.endsWith('Z') ? value : `${value}Z`);
  return date.toLocaleString(language === 'en' ? 'en-GB' : 'ar-EG', { dateStyle: 'medium', timeStyle: 'short' });
}

export function UserStatusBadge({ status }: { status: UserStatus }) {
  const { t } = useTranslation();
  const tone = status === 'Active' ? 'success' : status === 'Locked' ? 'error' : status === 'Suspended' ? 'warning' : 'neutral';
  return <Badge label={t(`security.status.${status}`)} tone={tone} />;
}

/** Page title row: heading on one side, actions on the other. */
export function PageHeader({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
      <h2 style={{ margin: 0 }}>{title}</h2>
      {children && <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>{children}</div>}
    </div>
  );
}

export function Hint({ children }: { children: ReactNode }) {
  return <p style={{ margin: 0, fontSize: 12.5, color: 'var(--color-text-muted)', lineHeight: 1.8 }}>{children}</p>;
}

export function Check({ checked, onChange, disabled, label }: { checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; label?: string }) {
  return (
    <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, cursor: disabled ? 'default' : 'pointer', fontSize: 13 }}>
      <input type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      {label}
    </label>
  );
}

export const tableStyle = { width: '100%', borderCollapse: 'collapse' as const, fontSize: 13 };
export const thStyle = { textAlign: 'start' as const, padding: '8px 10px', background: 'var(--color-surface-2, #f3f5f8)', borderBottom: '1px solid var(--color-border)', fontWeight: 600, whiteSpace: 'nowrap' as const };
export const tdStyle = { padding: '8px 10px', borderBottom: '1px solid var(--color-border)', verticalAlign: 'middle' as const };
