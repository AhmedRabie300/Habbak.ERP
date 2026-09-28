import { Icon } from './Icon';
import type { WorkspaceTab } from '../store/tabsStore';

interface TabBarProps {
  tabs: WorkspaceTab[];
  activeTabId: string | null;
  onActivate: (id: string) => void;
  onClose: (id: string) => void;
}

/** The tab strip above every screen — My Remarks/Remarks2.md, remark 3.4: several screens stay
 * open at once, each in its own closable tab, while the sidebar keeps controlling which new tab
 * opens next (see app/AppLayout.tsx). */
export function TabBar({ tabs, activeTabId, onActivate, onClose }: TabBarProps) {
  if (tabs.length === 0) return null;

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'stretch',
        gap: 2,
        overflowX: 'auto',
        background: 'var(--color-surface-2)',
        borderBottom: '1px solid var(--color-border)',
        padding: '6px 6px 0'
      }}
    >
      {tabs.map((tab) => {
        const isActive = tab.id === activeTabId;
        return (
          <div
            key={tab.id}
            role="button"
            tabIndex={0}
            onClick={() => onActivate(tab.id)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onActivate(tab.id);
              }
            }}
            title={tab.title}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 10px 8px 14px',
              maxWidth: 220,
              minWidth: 120,
              borderRadius: '10px 10px 0 0',
              background: isActive ? 'var(--color-surface)' : 'transparent',
              color: isActive ? 'var(--color-text)' : 'var(--color-text-muted)',
              fontWeight: isActive ? 700 : 500,
              fontSize: 13,
              cursor: 'pointer',
              userSelect: 'none',
              boxShadow: isActive ? '0 -1px 0 var(--color-gold-500) inset' : 'none'
            }}
          >
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', flex: 1 }}>{tab.title}</span>
            <span
              role="button"
              tabIndex={0}
              onClick={(e) => {
                e.stopPropagation();
                onClose(tab.id);
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  e.stopPropagation();
                  onClose(tab.id);
                }
              }}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: 18,
                height: 18,
                borderRadius: 'var(--radius)',
                flexShrink: 0,
                opacity: 0.65
              }}
              onMouseEnter={(e) => { e.currentTarget.style.background = 'var(--color-border)'; e.currentTarget.style.opacity = '1'; }}
              onMouseLeave={(e) => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.opacity = '0.65'; }}
            >
              <Icon name="x" size={12} />
            </span>
          </div>
        );
      })}
    </div>
  );
}
