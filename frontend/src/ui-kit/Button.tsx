import type { ButtonHTMLAttributes } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'success';
type Size = 'md' | 'sm';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
}

const variantStyles: Record<Variant, React.CSSProperties> = {
  primary: { background: 'var(--color-navy-700)', color: '#fff', border: '1px solid transparent', boxShadow: 'var(--shadow-brand)' },
  secondary: { background: 'var(--color-surface)', color: 'var(--color-navy-700)', border: '1px solid var(--color-border)' },
  danger: { background: 'var(--color-error)', color: '#fff', border: '1px solid transparent' },
  success: { background: 'var(--color-success)', color: '#fff', border: '1px solid transparent' },
  ghost: { background: 'transparent', color: 'var(--color-text-muted)', border: '1px solid transparent' }
};

const sizeStyles: Record<Size, React.CSSProperties> = {
  md: { padding: '9px 18px', fontSize: 13 },
  sm: { padding: '5px 12px', fontSize: 12 }
};

/** Standard button (00-Frontend-Specs.md, section 3.3) — pill shape + flat fills sourced from
 * the system's "الشريط الموسّع" (Ribbon) design pass, colors driven by the active color theme. */
export function Button({ variant = 'secondary', size = 'md', style, disabled, ...rest }: ButtonProps) {
  return (
    <button
      {...rest}
      disabled={disabled}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 6,
        ...variantStyles[variant],
        ...sizeStyles[size],
        borderRadius: 'var(--radius-pill)',
        fontWeight: 600,
        fontFamily: 'inherit',
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.55 : 1,
        whiteSpace: 'nowrap',
        transition: 'filter 0.15s, background 0.15s, border-color 0.15s',
        ...style
      }}
    />
  );
}
