import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from './Icon';
import { ConfirmModal } from './Modal';
import { actionAllowed, useButtonChecker, useCurrentScreenCode, useScreenRights } from '../features/auth/access';
import { printElement } from '../lib/export';

export interface ActionBarAction {
  key: string;
  label: string;
  onClick: () => void;
  /** Combined with the button's own permission check (00-System-Wide-Corrections-01.md, section 1.1). Defaults to true. */
  visible?: boolean;
  disabled?: boolean;
  /** Permission key checked via usePermission; omit for actions with no dedicated permission yet. */
  permissionKey?: string;
  /**
   * A special button (ButtonPermissionCatalog, e.g. 'Post'): its button permission decides instead
   * of the screen permission the key maps to. On the tab's screen unless buttonScreen says otherwise.
   */
  buttonCode?: string;
  buttonScreen?: string;
  /** Explicit icon override — inferred from `key` (save/post/delete/back/...) when omitted. */
  icon?: string;
}

export interface DestructiveAction extends ActionBarAction {
  /** Every destructive action must confirm before running (section 1.1) — defaults to true. */
  requiresConfirmation?: boolean;
  confirmTitle?: string;
  confirmMessage?: string;
}

interface ActionBarProps {
  /** The screen's single main positive action — Save/Post/Approve. */
  primary?: ActionBarAction;
  /** Non-destructive secondary actions — Back, Print, Export, Save as Draft. */
  secondary?: ActionBarAction[];
  /** Visually separated by a divider: destructive/dangerous actions — Delete, Cancel document, Reject. */
  destructive?: DestructiveAction[];
}

function isShown(action: ActionBarAction, allowed: boolean) {
  return (action.visible ?? true) && allowed;
}

type Group = 'primary' | 'secondary' | 'destructive';

/** Infers a sensible icon from the action's `key` so existing call sites don't need to specify
 * one explicitly — matches the icon vocabulary used across the module (save/post/delete/...). */
function inferIcon(key: string, group: 'primary' | 'secondary' | 'destructive'): string {
  const k = key.toLowerCase();
  if (k.includes('save')) return 'save';
  if (k.includes('post') || k.includes('approve') || k.includes('finish') || k.includes('settle') || k.includes('close')) return 'check';
  if ((k.includes('delete') || k.includes('remove')) && group === 'destructive') return 'trash';
  if (k.includes('cancel') && group === 'destructive') return 'trash';
  if (k.includes('reverse') || k.includes('reopen')) return 'reverse';
  if (k.includes('back') || k.includes('cancel')) return 'x';
  if (k.includes('export')) return 'download';
  if (k.includes('import')) return 'upload';
  if (k.includes('print')) return 'printer';
  if (k.includes('add') || k.includes('link') || k.includes('new') || k.includes('open') || k.includes('issue')) return 'plus';
  return group === 'destructive' ? 'trash' : group === 'primary' ? 'check' : 'plus';
}

/**
 * The single standard place for a non-List screen's action buttons — a ribbon-style toolbar
 * fixed at the TOP of the screen (00-System-Wide-Corrections-01.md, section 1), not a footer bar.
 * Icon-over-label buttons, grouped and separated by dividers. No screen builds its own action
 * bar: each only supplies its list of buttons via props; this component alone controls order,
 * icons, color and confirmation behavior.
 */
export function ActionBar({ primary, secondary = [], destructive = [] }: ActionBarProps) {
  const { t } = useTranslation();
  const [pendingConfirm, setPendingConfirm] = useState<DestructiveAction | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  // The user's rights on this tab's screen, read once (Rules of Hooks); each action is then checked
  // against the permission it needs — see actionAllowed for the key → permission mapping.
  const rights = useScreenRights();
  const screenCode = useCurrentScreenCode();
  const buttonAllowed = useButtonChecker();
  const allowed = (a: ActionBarAction, group: Group) =>
    isShown(a, a.buttonCode ? buttonAllowed(a.buttonScreen ?? screenCode, a.buttonCode) : actionAllowed(rights, a.key, a.permissionKey, group));

  const visibleSecondary = secondary.filter((a) => allowed(a, 'secondary'));
  const visibleDestructive = destructive.filter((a) => allowed(a, 'destructive'));
  const showPrimary = primary && allowed(primary, 'primary');

  // My Remarks/Remarks2.md, remark 5.2 — every screen that renders an ActionBar gets a Print
  // button for free: it prints the screen's own top-level wrapper (this bar's DOM parent), minus
  // this bar itself (tagged data-no-print below) and anything else so tagged.
  const handlePrint = () => {
    const screenRoot = containerRef.current?.parentElement;
    if (!screenRoot) return;
    const title = screenRoot.querySelector('h2')?.textContent?.trim() || document.title;
    printElement(screenRoot, title);
  };

  return (
    <>
      <div
        ref={containerRef}
        data-no-print="true"
        style={{
          background: 'var(--color-surface)',
          borderTop: '3px solid var(--color-gold-500)',
          borderRadius: 'var(--radius-lg)',
          boxShadow: 'var(--shadow-2)',
          padding: '10px 18px',
          marginBottom: 16,
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          flexWrap: 'wrap'
        }}
      >
        {visibleDestructive.length > 0 && (
          <>
            {visibleDestructive.map((a) => (
              <ToolbarButton
                key={a.key}
                icon={a.icon ?? inferIcon(a.key, 'destructive')}
                label={a.label}
                tone="danger"
                disabled={a.disabled}
                onClick={() => ((a.requiresConfirmation ?? true) ? setPendingConfirm(a) : a.onClick())}
              />
            ))}
            <Divider />
          </>
        )}

        {visibleSecondary.map((a) => (
          <ToolbarButton key={a.key} icon={a.icon ?? inferIcon(a.key, 'secondary')} label={a.label} disabled={a.disabled} onClick={a.onClick} />
        ))}

        {rights.print && <ToolbarButton key="print" icon="printer" label={t('common.print')} onClick={handlePrint} />}

        {showPrimary && (
          <>
            {visibleSecondary.length > 0 && <Divider />}
            <ToolbarButton icon={primary!.icon ?? inferIcon(primary!.key, 'primary')} label={primary!.label} tone="primary" disabled={primary!.disabled} onClick={primary!.onClick} />
          </>
        )}
      </div>

      <ConfirmModal
        open={pendingConfirm !== null}
        title={pendingConfirm?.confirmTitle ?? t('common.delete')}
        message={pendingConfirm?.confirmMessage}
        confirmLabel={pendingConfirm?.label}
        onCancel={() => setPendingConfirm(null)}
        onConfirm={() => {
          pendingConfirm?.onClick();
          setPendingConfirm(null);
        }}
      />
    </>
  );
}

function Divider() {
  return <div style={{ width: 1, alignSelf: 'stretch', background: 'var(--color-border)', margin: '2px 8px' }} />;
}

function ToolbarButton({
  icon, label, tone, disabled, onClick
}: { icon: string; label: string; tone?: 'primary' | 'danger'; disabled?: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      style={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 3,
        border: 'none',
        borderRadius: 'var(--radius)',
        padding: '6px 14px',
        fontFamily: 'inherit',
        fontSize: 11,
        fontWeight: 600,
        cursor: disabled ? 'not-allowed' : 'pointer',
        opacity: disabled ? 0.5 : 1,
        background: tone === 'primary' ? 'var(--color-navy-700)' : 'transparent',
        color: tone === 'primary' ? '#fff' : tone === 'danger' ? 'var(--color-error)' : 'var(--color-text)',
        transition: 'background 0.15s'
      }}
      onMouseEnter={(e) => { if (!disabled && !tone) e.currentTarget.style.background = 'var(--color-surface-2)'; }}
      onMouseLeave={(e) => { if (!tone) e.currentTarget.style.background = 'transparent'; }}
    >
      <Icon name={icon as never} size={18} />
      <span>{label}</span>
    </button>
  );
}
