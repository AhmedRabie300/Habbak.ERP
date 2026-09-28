export interface SectionTabItem {
  id: string;
  label: string;
  badge?: number;
}

interface SectionTabsProps {
  items: SectionTabItem[];
  activeId: string;
  onChange: (id: string) => void;
}

/** In-page section tabs (بيانات/شخصية/عقود/مستندات/شهادات على شاشة الموظف، Docs/Implementation/HR-MASTER-PLAN.md
 * §Phase 1.5, Sub-Batch 1.5.1) — أول Component من نوعه في المشروع؛ لا يلتبس بـ`TabBar` (شريط المستندات
 * المفتوحة فوق الصفحة كلها، `app/AppLayout.tsx`). Presentational بس — الأب هو اللي بيقرر أي محتوى يعرض
 * لكل تبويب (زي `activeId`/`onChange` بتاعة أي Controlled Component تاني في `ui-kit`). */
export function SectionTabs({ items, activeId, onChange }: SectionTabsProps) {
  return (
    <div role="tablist" style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--color-border)', padding: '0 4px' }}>
      {items.map((item) => {
        const isActive = item.id === activeId;
        return (
          <button
            key={item.id}
            type="button"
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(item.id)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '10px 16px',
              border: 'none',
              borderBottom: isActive ? '2px solid var(--color-gold-500)' : '2px solid transparent',
              background: 'transparent',
              color: isActive ? 'var(--color-text)' : 'var(--color-text-muted)',
              fontWeight: isActive ? 700 : 500,
              fontSize: 13,
              cursor: 'pointer'
            }}
          >
            {item.label}
            {item.badge !== undefined && item.badge > 0 && (
              <span
                style={{
                  display: 'inline-flex',
                  minWidth: 18,
                  height: 18,
                  padding: '0 5px',
                  alignItems: 'center',
                  justifyContent: 'center',
                  borderRadius: 'var(--radius-chip)',
                  background: 'var(--color-surface-2)',
                  fontSize: 11,
                  fontWeight: 700
                }}
              >
                {item.badge}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
