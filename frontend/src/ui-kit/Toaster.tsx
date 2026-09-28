import { useToastStore } from '../store/toastStore';

const toneColors: Record<string, string> = {
  error: 'var(--color-error)',
  success: 'var(--color-success)',
  info: 'var(--color-navy-700)'
};

/** Renders every active Toast (00-Frontend-Specs.md, section 3.3) — mounted once at the app root. */
export function Toaster() {
  const toasts = useToastStore((s) => s.toasts);
  const dismiss = useToastStore((s) => s.dismiss);

  return (
    <div style={{ position: 'fixed', bottom: 16, insetInlineEnd: 16, display: 'flex', flexDirection: 'column', gap: 8, zIndex: 1000 }}>
      {toasts.map((t) => (
        <div
          key={t.id}
          onClick={() => dismiss(t.id)}
          style={{
            background: '#fff',
            borderInlineStart: `4px solid ${toneColors[t.variant]}`,
            boxShadow: 'var(--shadow-2)',
            borderRadius: 'var(--radius)',
            padding: '10px 14px',
            minWidth: 260,
            maxWidth: 360,
            fontSize: 13,
            cursor: 'pointer'
          }}
        >
          {t.message}
        </div>
      ))}
    </div>
  );
}
