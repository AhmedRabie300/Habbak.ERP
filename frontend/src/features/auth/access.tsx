import { createContext, useContext, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useMyAccess, type ScreenRights } from './api';

const FULL: ScreenRights = { view: true, add: true, edit: true, delete: true, print: true, export: true, approve: true };
const NONE: ScreenRights = { view: false, add: false, edit: false, delete: false, print: false, export: false, approve: false };

/** The screen (MenuItem code) a tab belongs to — set by AppLayout around every tab. */
const ScreenCodeContext = createContext<string | null>(null);

export function ScreenCodeProvider({ code, children }: { code: string | null; children: ReactNode }) {
  return <ScreenCodeContext.Provider value={code}>{children}</ScreenCodeContext.Provider>;
}

/**
 * Which screen a path belongs to: the screen route it equals or sits under (longest wins), so
 * /sales/invoices/12 belongs to /sales/invoices. Resolved against the full permission catalog, not
 * the (permission-filtered) menu — a screen hidden from the menu must still be recognised to be
 * refused. A path under no screen route (a receipt, a payment page) belongs to none: those are
 * reached from a screen that was already checked, and the API checks every call anyway.
 */
export function resolveScreenCode(path: string, screens: { code: string; routeKey: string }[]): string | null {
  let best: { code: string; length: number } | null = null;
  for (const screen of screens) {
    const route = screen.routeKey;
    if ((path === route || path.startsWith(`${route}/`)) && (!best || route.length > best.length)) {
      best = { code: screen.code, length: route.length };
    }
  }
  return best?.code ?? null;
}

/**
 * The current user's rights on the current tab's screen. Everything is allowed while rights are
 * loading and outside any screen: hiding a button is a convenience — the server enforces the rule.
 */
export function useScreenRights(): ScreenRights {
  const code = useContext(ScreenCodeContext);
  const { data } = useMyAccess();
  if (!data || !code || data.isFullAccess) return FULL;
  return data.screens[code] ?? NONE;
}

/** The current tab's screen code (null outside a screen, e.g. the check being sold). */
export function useCurrentScreenCode(): string | null {
  return useContext(ScreenCodeContext);
}

/**
 * Whether the current user may view / edit a sensitive field (FieldPermissionCatalog) on a screen —
 * the same field may be open on one screen and hidden on another. Open while rights load and for
 * a field with no rule; the server masks and ignores it regardless.
 */
export function useFieldPermission(screenCode: string, entityType: string, fieldName: string, action: 'view' | 'edit' = 'view'): boolean {
  const { data } = useMyAccess();
  if (!data || data.isFullAccess) return true;
  const rights = data.fieldPermissions[screenCode]?.[entityType]?.[fieldName];
  if (!rights) return true;
  return action === 'view' ? rights.canView : rights.canEdit;
}

/**
 * The same as useFieldPermission for several fields of one entity on one screen (Rules of Hooks):
 * returns field → { canView, canEdit }. A form hides what may not be viewed (the server sends it
 * empty) and locks what may not be edited (the server keeps the stored value regardless).
 */
export function useFieldAccess(screenCode: string, entityType: string): (fieldName: string) => { canView: boolean; canEdit: boolean } {
  const { data } = useMyAccess();
  return (fieldName) => {
    if (!data || data.isFullAccess) return { canView: true, canEdit: true };
    const rights = data.fieldPermissions[screenCode]?.[entityType]?.[fieldName];
    return { canView: rights?.canView ?? true, canEdit: rights?.canEdit ?? true };
  };
}

/** A checker for many buttons at once (Rules of Hooks) — see useButtonPermission. */
export function useButtonChecker(): (screenCode: string | null, buttonCode: string) => boolean {
  const { data } = useMyAccess();
  return (screenCode, buttonCode) => {
    if (!data || data.isFullAccess || !screenCode) return true;
    return data.buttonPermissions[screenCode]?.[buttonCode] ?? true;
  };
}

/**
 * Whether the current user may press a special button (ButtonPermissionCatalog) on a screen: the
 * role's button permission, else the screen permission the button falls back on — worked out by
 * the server in /auth/me. Hiding is a convenience; the API refuses the press regardless (except the
 * few buttons that never reach the server, e.g. reprint).
 */
export function useButtonPermission(screenCode: string, buttonCode: string): boolean {
  return useButtonChecker()(screenCode, buttonCode);
}

/**
 * A tab whose screen the user may not open shows this instead — the menu already hides such
 * screens; this covers a tab restored from an earlier session or a link typed by hand.
 * "/new" additionally needs Add.
 */
export function ScreenGuard({ path, children }: { path: string; children: ReactNode }) {
  const { t } = useTranslation();
  const code = useContext(ScreenCodeContext);
  const { data } = useMyAccess();

  if (!data || !code || data.isFullAccess) return <>{children}</>;
  const rights = data.screens[code] ?? NONE;
  const allowed = rights.view && (!path.endsWith('/new') || rights.add);
  if (allowed) return <>{children}</>;

  return (
    <div style={{ padding: 48, textAlign: 'center', color: 'var(--color-text-muted)' }}>
      <div style={{ fontSize: 40, marginBottom: 12 }}>🔒</div>
      <h2 style={{ margin: '0 0 8px', color: 'var(--color-text)' }}>{t('auth.noAccessTitle')}</h2>
      <p style={{ margin: 0 }}>{t('auth.noAccessBody')}</p>
    </div>
  );
}

/**
 * Maps an ActionBar action to the permission it needs — the same rule the server applies: an
 * explicit permissionKey ('view' | 'add' | 'edit' | 'delete' | 'print' | 'export' | 'approve'),
 * else the action's key: delete/remove → Delete; post/approve/reject/reverse/settle/reopen, and
 * cancel as a document action → Approve; back / cancel-editing → always; anything else (save,
 * submit…) → Add or Edit.
 */
export function actionAllowed(
  rights: ScreenRights, actionKey: string, permissionKey: string | undefined, group: 'primary' | 'secondary' | 'destructive'
): boolean {
  const key = (permissionKey ?? actionKey).toLowerCase();
  if (key in rights) return rights[key as keyof ScreenRights];
  if (/(delete|remove)/.test(key)) return rights.delete;
  if (/(back|refresh|preview)/.test(key) || (group === 'secondary' && key.includes('cancel'))) return true;
  if (/(post|approve|reject|reverse|cancel|settle|reopen|award)/.test(key)) return rights.approve;
  if (key.includes('export')) return rights.export;
  if (key.includes('print')) return rights.print;
  return rights.add || rights.edit;
}
