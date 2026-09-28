import type { ReactNode } from 'react';

type Tone = 'info' | 'success' | 'warning' | 'error';

interface AlertProps {
  tone?: Tone;
  children: ReactNode;
}

const toneStyles: Record<Tone, { bg: string; fg: string; border: string }> = {
  info: { bg: 'var(--color-info-bg)', fg: 'var(--color-info-fg)', border: 'var(--color-navy-500)' },
  success: { bg: 'var(--color-success-bg)', fg: 'var(--color-success)', border: 'var(--color-success)' },
  warning: { bg: 'var(--color-warning-bg)', fg: 'var(--color-warning)', border: 'var(--color-warning)' },
  error: { bg: 'var(--color-error-bg)', fg: 'var(--color-error)', border: 'var(--color-error)' }
};

/** Standard inline banner (design language ported from the NozomSoft reference project's
 * `.alert` classes) — replaces the ad-hoc gold-bordered "Notice" divs scattered across screens. */
export function Alert({ tone = 'info', children }: AlertProps) {
  const colors = toneStyles[tone];
  return (
    <div
      style={{
        padding: '12px 16px',
        borderRadius: 'var(--radius-chip)',
        fontSize: 13,
        background: colors.bg,
        color: colors.fg,
        border: `1px solid ${colors.border}33`
      }}
    >
      {children}
    </div>
  );
}
