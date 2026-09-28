import type { FocusEvent, InputHTMLAttributes, ReactNode } from 'react';

interface FieldWrapperProps {
  label: string;
  error?: string;
  children: ReactNode;
}

/** Wraps any input with a label and inline validation message (00-Frontend-Specs.md, section 12.2). */
export function FieldWrapper({ label, error, children }: FieldWrapperProps) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 13 }}>
      <span style={{ color: 'var(--color-text-muted)', fontWeight: 600 }}>{label}</span>
      {children}
      {error && <span style={{ color: 'var(--color-error)', fontSize: 12 }}>{error}</span>}
    </label>
  );
}

/** Filled inputs with a border that only appears on focus — from the system's "الشريط الموسّع"
 * (Ribbon) design pass, colors driven by the active color theme. */
const inputStyle: React.CSSProperties = {
  padding: '9px 12px',
  borderRadius: 'var(--radius-chip)',
  border: '1px solid transparent',
  background: 'var(--color-surface-2)',
  fontSize: 14,
  fontFamily: 'inherit',
  outline: 'none',
  transition: 'border-color 0.15s, background 0.15s'
};

function handleFocus(e: FocusEvent<HTMLInputElement | HTMLSelectElement>) {
  e.currentTarget.style.borderColor = 'var(--color-gold-500)';
  e.currentTarget.style.background = 'var(--color-surface)';
}

function handleBlur(e: FocusEvent<HTMLInputElement | HTMLSelectElement>) {
  if (!e.currentTarget.classList.contains('field-error')) {
    e.currentTarget.style.borderColor = 'transparent';
  }
  e.currentTarget.style.background = 'var(--color-surface-2)';
}

export function Input(props: InputHTMLAttributes<HTMLInputElement> & { error?: boolean }) {
  const { error, style, onFocus, onBlur, className, ...rest } = props;
  return (
    <input
      {...rest}
      className={error ? `field-error ${className ?? ''}` : className}
      onFocus={(e) => { handleFocus(e); onFocus?.(e); }}
      onBlur={(e) => { handleBlur(e); onBlur?.(e); }}
      style={{ ...inputStyle, borderColor: error ? 'var(--color-error)' : 'transparent', ...style }}
    />
  );
}

/** No native `<select>` in this system — every dropdown is the searchable
 * SearchableSelect/SearchableMultiSelect instead (00-System-Wide-Corrections-02.md, section 1). */
