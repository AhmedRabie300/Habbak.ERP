import { useEffect, useMemo, useState } from 'react';
import { Routes, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../ui-kit/Button';
import { TabBar } from '../ui-kit/TabBar';
import { useAppStore, THEMES } from '../store/appStore';
import { useToastStore } from '../store/toastStore';
import { useTabsStore } from '../store/tabsStore';
import { useQueryClient } from '@tanstack/react-query';
import { useMenuTree, type MenuTreeNode } from '../features/navigation/api';
import { logout as endSession } from '../features/auth/api';
import { scopeLabel } from '../features/auth/LoginPage';
import { resolveScreenCode, ScreenCodeProvider, ScreenGuard } from '../features/auth/access';
import { usePermissionCatalog } from '../features/settings/security/api';
import { SearchableSelect } from '../ui-kit/SearchableSelect';
import { refreshSession, setActiveScreenCode } from './api';
import { appRoutes, DEFAULT_PATH } from './routeTable';
import { NotificationBell } from '../features/notifications/NotificationBell';

const navItemStyle = ({ isActive }: { isActive: boolean }): React.CSSProperties => ({
  padding: '10px 16px',
  color: isActive ? '#fff' : '#c9d3e6',
  background: isActive ? 'var(--color-navy-500)' : 'transparent',
  borderRadius: 8,
  textDecoration: 'none',
  // Remarks4 item 1: the sidebar is read all day — a screen name sits at 16px, its group at 14.
  fontSize: 16,
  border: 'none',
  width: '100%',
  textAlign: 'start',
  cursor: 'pointer',
  fontFamily: 'inherit'
});

interface MenuLeaf {
  routeKey: string;
  name: string;
}

function flattenMenu(nodes: MenuTreeNode[]): MenuLeaf[] {
  const leaves: MenuLeaf[] = [];
  for (const node of nodes) {
    if (node.routeKey) leaves.push({ routeKey: node.routeKey, name: node.name });
    if (node.children.length) leaves.push(...flattenMenu(node.children));
  }
  return leaves;
}

/** Longest-prefix match against the menu's own leaf routes, so an Edit screen (nested under its
 * List's path) inherits that screen's name — with the record id (or "new") appended so several
 * tabs of the same screen stay distinguishable. */
function resolveTabTitle(pathname: string, leaves: MenuLeaf[], newRecordLabel: string): string {
  const match = leaves
    .filter((l) => pathname === l.routeKey || pathname.startsWith(`${l.routeKey}/`))
    .sort((a, b) => b.routeKey.length - a.routeKey.length)[0];
  if (!match) return pathname;
  const rest = pathname.slice(match.routeKey.length).replace(/^\//, '');
  if (!rest) return match.name;
  if (rest === 'new') return `${match.name} · ${newRecordLabel}`;
  return `${match.name} · ${rest}`;
}

/** Root shell: side nav + the module's screens, following the standard List/Search/Edit routing pattern. */
/** Whether any screen under this node is the one currently open. */
function containsPath(node: MenuTreeNode, activePath: string | null): boolean {
  if (!activePath) return false;
  if (node.routeKey && (activePath === node.routeKey || activePath.startsWith(`${node.routeKey}/`))) return true;
  return node.children.some((child) => containsPath(child, activePath));
}

function MenuNode({ node, activePath, onOpen }: { node: MenuTreeNode; activePath: string | null; onOpen: (path: string, title: string) => void }) {
  // Remarks4 item 2: groups start closed so the whole system fits on one screen; the group holding
  // the open screen opens itself, and stays wherever the user last put it afterwards.
  const holdsActive = containsPath(node, activePath);
  const [isExpanded, setIsExpanded] = useState(holdsActive);
  const [touched, setTouched] = useState(false);
  useEffect(() => {
    if (holdsActive && !touched) setIsExpanded(true);
  }, [holdsActive, touched]);

  const toggle = () => {
    setTouched(true);
    setIsExpanded((v) => !v);
  };

  if (node.routeKey) {
    const isActive = activePath === node.routeKey || (activePath?.startsWith(`${node.routeKey}/`) ?? false);
    return (
      <button type="button" onClick={() => onOpen(node.routeKey!, node.name)} style={navItemStyle({ isActive })}>
        {node.name}
      </button>
    );
  }

  // A route-less node is a pure group header (00-System-Wide-Corrections-01.md, section 3.1) —
  // clickable to expand/collapse its children; starts expanded so first-load behavior is unchanged.
  return (
    <>
      <div
        role="button"
        tabIndex={0}
        onClick={toggle}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            toggle();
          }
        }}
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          fontSize: 14,
          fontWeight: 600,
          color: '#c9d3e6',
          marginBottom: 4,
          marginTop: 8,
          padding: '4px 6px',
          borderRadius: 6,
          cursor: 'pointer',
          userSelect: 'none'
        }}
      >
        <span>{node.name}</span>
        <span
          aria-hidden
          style={{
            fontSize: 12,
            transition: 'transform 0.15s',
            transform: isExpanded ? 'rotate(0deg)' : 'rotate(-90deg)'
          }}
        >
          ▾
        </span>
      </div>
      {isExpanded && node.children.map((child) => (
        <MenuNode key={child.id} node={child} activePath={activePath} onOpen={onOpen} />
      ))}
    </>
  );
}

export function AppLayout() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const companyId = useAppStore((s) => s.companyId);
  const fullName = useAppStore((s) => s.fullName);
  const scopes = useAppStore((s) => s.scopes);
  const queryClient = useQueryClient();
  const { data: catalog } = usePermissionCatalog();
  const [switching, setSwitching] = useState(false);
  const toggleLanguage = useAppStore((s) => s.toggleLanguage);
  const theme = useAppStore((s) => s.theme);
  const setTheme = useAppStore((s) => s.setTheme);
  const showToast = useToastStore((s) => s.show);
  const { data: menu } = useMenuTree();

  const tabs = useTabsStore((s) => s.tabs);
  const activeTabId = useTabsStore((s) => s.activeTabId);
  const openTab = useTabsStore((s) => s.openTab);
  const trackNavigation = useTabsStore((s) => s.trackNavigation);
  const closeTab = useTabsStore((s) => s.closeTab);
  const setActiveTab = useTabsStore((s) => s.setActiveTab);

  const menuLeaves = useMemo(() => flattenMenu(menu ?? []), [menu]);
  const activeTab = tabs.find((tb) => tb.id === activeTabId) ?? null;
  // Set while rendering, not in an effect: a newly opened tab's own queries start in its effects,
  // which run before this component's — they must already carry the new screen.
  setActiveScreenCode(activeTab ? resolveScreenCode(activeTab.path, catalog?.screens ?? []) : null);

  // Every real navigation (sidebar included) lands here: either it matches a tab already open
  // elsewhere (just switch to it) or it's drilled into from the active tab, so that tab's own
  // path/title update in place (My Remarks/Remarks2.md, remark 3.4 — a screen's own List → Edit →
  // Back flow stays the SAME tab; only the sidebar starts a genuinely new one, via handleOpenTab).
  useEffect(() => {
    if (location.pathname === '/') {
      if (!menu) return;
      navigate(menuLeaves[0]?.routeKey ?? DEFAULT_PATH, { replace: true });
      return;
    }
    trackNavigation(location.pathname, resolveTabTitle(location.pathname, menuLeaves, t('common.newRecord')));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [location.pathname, menu]);

  // Once the (async-loaded, language-dependent) menu is available, backfill every open tab's
  // title from it — fixes the very first tab's title (raw pathname, resolved before the menu
  // loaded) and re-labels every tab when the user toggles language.
  useEffect(() => {
    if (menuLeaves.length === 0) return;
    useTabsStore.setState((state) => ({
      tabs: state.tabs.map((tb) => ({ ...tb, title: resolveTabTitle(tb.path, menuLeaves, t('common.newRecord')) }))
    }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [menuLeaves]);

  const handleOpenTab = (path: string, title: string) => {
    openTab(path, title);
    navigate(path);
  };

  const handleActivateTab = (id: string) => {
    const tab = tabs.find((tb) => tb.id === id);
    if (!tab) return;
    setActiveTab(id);
    navigate(tab.path);
  };

  const handleCloseTab = (id: string) => {
    closeTab(id);
    const next = useTabsStore.getState();
    const nextTab = next.tabs.find((tb) => tb.id === next.activeTabId);
    navigate(nextTab?.path ?? DEFAULT_PATH);
  };

  // Switching company or branch = a new session there: its own roles, menu and data.
  const branchId = useAppStore((s) => s.branchId);
  const currentKey = `${companyId}:${branchId ?? ''}`;
  const switchScope = async (key: string) => {
    if (key === currentKey) return;
    const [companyPart, branchPart] = key.split(':');
    setSwitching(true);
    const session = await refreshSession(Number(companyPart), branchPart ? Number(branchPart) : null);
    setSwitching(false);
    if (session) {
      useTabsStore.setState({ tabs: [], activeTabId: null });
      queryClient.clear();
      navigate('/', { replace: true });
    } else {
      showToast(t('auth.switchFailed'), 'error');
    }
  };

  const current = scopes.find((x) => x.companyId === companyId && x.branchId === branchId) ?? scopes.find((x) => x.companyId === companyId);

  return (
    <div style={{ display: 'flex', minHeight: '100vh' }}>
      <aside style={{ width: 240, background: 'var(--color-sidebar-gradient)', color: '#fff', display: 'flex', flexDirection: 'column', padding: 16, gap: 4 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
          <img src="/logo.jpeg" alt="الحبّاك" style={{ width: 40, height: 40, borderRadius: 8, objectFit: 'cover' }} />
          <div>
            <div style={{ fontWeight: 700, fontSize: 16, color: 'var(--color-gold-500)', lineHeight: 1.2 }}>{t('common.appName')}</div>
            <div style={{ fontSize: 11, color: '#c9d3e6' }}>{t('common.slogan')}</div>
          </div>
        </div>
        <div style={{ height: 1, background: 'rgba(255,255,255,0.1)', margin: '4px 0 12px' }} />

        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, overflowY: 'auto' }}>
          {menu?.map((node) => <MenuNode key={node.id} node={node} activePath={activeTab?.path ?? null} onOpen={handleOpenTab} />)}
        </div>

        <div style={{ marginTop: 'auto', display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ background: 'rgba(255,255,255,0.06)', borderRadius: 8, padding: '8px 10px', display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: '#fff' }}>👤 {fullName ?? '—'}</div>
            {scopes.length > 1 ? (
              <SearchableSelect
                id="company-switcher"
                value={currentKey}
                onChange={(v) => void switchScope(String(v))}
                disabled={switching}
                options={scopes.map((sc) => ({ value: `${sc.companyId}:${sc.branchId ?? ''}`, label: scopeLabel(sc, i18n.language, t) }))}
              />
            ) : (
              <div style={{ fontSize: 11, color: '#8a97b3' }}>{current ? scopeLabel(current, i18n.language, t) : companyId ?? '—'}</div>
            )}
            <button
              type="button"
              onClick={() => navigate('/change-password')}
              style={{ background: 'none', border: 'none', color: '#c9d3e6', fontSize: 12, textAlign: 'start', padding: 0, cursor: 'pointer', fontFamily: 'inherit' }}
            >
              🔑 {t('auth.changePassword')}
            </button>
            <button
              type="button"
              onClick={() => navigate('/two-factor')}
              style={{ background: 'none', border: 'none', color: '#c9d3e6', fontSize: 12, textAlign: 'start', padding: 0, cursor: 'pointer', fontFamily: 'inherit' }}
            >
              🛡️ {t('auth.twoFactorTitle')}
            </button>
          </div>
          <Button variant="secondary" onClick={toggleLanguage}>🌐 {t('common.language')}</Button>
          <div>
            <div style={{ fontSize: 11, color: '#8a97b3', marginBottom: 4 }}>🎨 {t('common.theme')}</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {THEMES.map((th) => (
                <button
                  key={th.value}
                  type="button"
                  title={i18n.language === 'ar' ? th.labelAr : th.labelEn}
                  onClick={() => setTheme(th.value)}
                  style={{
                    width: 22,
                    height: 22,
                    borderRadius: '50%',
                    background: th.swatch,
                    border: theme === th.value ? '2px solid #fff' : '2px solid transparent',
                    boxShadow: theme === th.value ? '0 0 0 2px var(--color-gold-500)' : 'none',
                    cursor: 'pointer',
                    padding: 0
                  }}
                />
              ))}
            </div>
          </div>
          <Button
            variant="ghost"
            style={{ color: '#c9d3e6' }}
            onClick={async () => {
              await endSession();
              useTabsStore.setState({ tabs: [], activeTabId: null });
              queryClient.clear();
              navigate('/login', { replace: true });
            }}
          >
            {t('common.logout')}
          </Button>
        </div>
      </aside>

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
        {/* Docs/Implementation/Phase-2.5-Research.md §1.2 — a genuine top Navbar didn't exist
            before this (everything else — user name, settings, logout — already lives in the
            sidebar and stays there); added here only to carry the notification bell, which needs
            to sit above every open tab, not inside any one of them. */}
        <div
          style={{
            height: 48, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'flex-end',
            padding: '0 16px', borderBottom: '1px solid var(--color-border)', background: 'var(--color-surface)'
          }}
        >
          <NotificationBell />
        </div>
        <TabBar tabs={tabs} activeTabId={activeTabId} onActivate={handleActivateTab} onClose={handleCloseTab} />
        <main style={{ flex: 1, padding: 24, minWidth: 0 }}>
          {tabs.map((tab) => (
            <div key={tab.id} style={{ display: tab.id === activeTabId ? 'block' : 'none', minWidth: 0 }}>
              <ScreenCodeProvider code={resolveScreenCode(tab.path, catalog?.screens ?? [])}>
                <ScreenGuard path={tab.path}>
                  <Routes location={{ pathname: tab.path, search: '', hash: '', state: null, key: tab.id }}>
                    {appRoutes}
                  </Routes>
                </ScreenGuard>
              </ScreenCodeProvider>
            </div>
          ))}
        </main>
      </div>
    </div>
  );
}
