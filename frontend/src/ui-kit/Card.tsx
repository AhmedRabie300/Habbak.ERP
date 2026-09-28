import type { ReactNode } from 'react';

interface CardProps {
  children: ReactNode;
  style?: React.CSSProperties;
}

/** Standard content container (design language ported from the NozomSoft reference project's
 * `.card`/`.card-header`/`.card-body` — colors stay on الحبّاك's navy/gold tokens). Replaces the
 * ad-hoc `<section style={{background:'#fff', ...}}>` wrapper repeated across every screen. */
export function Card({ children, style }: CardProps) {
  return (
    <section
      style={{
        background: 'var(--color-surface)',
        borderTop: '3px solid var(--color-gold-500)',
        borderRadius: 'var(--radius-lg)',
        boxShadow: 'var(--shadow-2)',
        ...style
      }}
    >
      {children}
    </section>
  );
}

interface CardHeaderProps {
  children: ReactNode;
  end?: ReactNode;
}

export function CardHeader({ children, end }: CardHeaderProps) {
  return (
    <div
      style={{
        padding: '14px 20px',
        borderBottom: '1px solid var(--color-border)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        fontWeight: 700,
        fontSize: 15
      }}
    >
      <div>{children}</div>
      {end && <div style={{ display: 'flex', gap: 8 }}>{end}</div>}
    </div>
  );
}

export function CardBody({ children, style }: CardProps) {
  return <div style={{ padding: 20, ...style }}>{children}</div>;
}
