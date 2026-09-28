import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../../ui-kit/Button';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { useAppStore } from '../../store/appStore';
import { authErrorMessage, changePassword, logout } from './api';

/**
 * Changing your own password. Reached on purpose from the sidebar, or forced: while the session is
 * flagged "password change required" (first sign-in, reset by an admin, or expired) the API
 * refuses everything else, and the app sends the user here.
 */
export function ChangePasswordPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setSession = useAppStore((s) => s.setSession);
  const forced = useAppStore((s) => s.passwordChangeRequired);
  const fullName = useAppStore((s) => s.fullName);
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (next !== confirm) {
      setError(t('auth.passwordsDoNotMatch'));
      return;
    }

    setBusy(true);
    setError(null);
    try {
      setSession(await changePassword(current, next));
      navigate('/', { replace: true });
    } catch (err) {
      setError(authErrorMessage(err, t('auth.changeFailed')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', background: 'var(--color-bg, #f4f6f9)', padding: 16 }}>
      <form
        onSubmit={submit}
        style={{ width: '100%', maxWidth: 400, display: 'flex', flexDirection: 'column', gap: 16, background: 'var(--color-surface, #fff)', padding: 28, borderRadius: 14, boxShadow: 'var(--shadow-2)', borderTop: '4px solid var(--color-gold-500)' }}
      >
        <h2 style={{ margin: 0, fontSize: 18 }}>{t('auth.changePassword')}</h2>
        <p style={{ margin: 0, fontSize: 13, color: 'var(--color-text-muted)', lineHeight: 1.7 }}>
          {forced ? t('auth.changeRequired', { name: fullName ?? '' }) : t('auth.changeHint')}
        </p>

        <FieldWrapper label={t('auth.currentPassword')}>
          <Input id="cp-current" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" dir="ltr" autoFocus />
        </FieldWrapper>
        <FieldWrapper label={t('auth.newPassword')}>
          <Input id="cp-new" type="password" value={next} onChange={(e) => setNext(e.target.value)} autoComplete="new-password" dir="ltr" />
        </FieldWrapper>
        <FieldWrapper label={t('auth.confirmPassword')}>
          <Input id="cp-confirm" type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} autoComplete="new-password" dir="ltr" />
        </FieldWrapper>

        {error && (
          <div role="alert" style={{ background: 'var(--color-error-bg)', color: 'var(--color-error)', padding: '10px 12px', borderRadius: 8, fontSize: 13, lineHeight: 1.7 }}>
            {error}
          </div>
        )}

        <div style={{ display: 'flex', gap: 8 }}>
          <Button type="submit" variant="primary" disabled={busy || !current || !next || !confirm} style={{ flex: 1 }}>
            {busy ? t('common.loading') : t('auth.savePassword')}
          </Button>
          <Button
            type="button"
            variant="ghost"
            onClick={async () => {
              if (forced) {
                await logout();
                navigate('/login', { replace: true });
              } else {
                navigate(-1);
              }
            }}
          >
            {forced ? t('common.logout') : t('common.back')}
          </Button>
        </div>
      </form>
    </div>
  );
}
