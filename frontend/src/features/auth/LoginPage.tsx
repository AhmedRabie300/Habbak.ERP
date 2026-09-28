import { useState, type FormEvent } from 'react';
import axios from 'axios';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '../../ui-kit/Button';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { SearchableSelect } from '../../ui-kit/SearchableSelect';
import { useAppStore, type AuthSession } from '../../store/appStore';
import { refreshSession } from '../../app/api';
import { useTabsStore } from '../../store/tabsStore';
import { authErrorMessage, isTwoFactorChallenge, login, loginTwoFactor, type TwoFactorChallenge } from './api';

function axiosErrorCode(error: unknown): string | undefined {
  return axios.isAxiosError(error) ? (error.response?.data as { errorCode?: string } | undefined)?.errorCode : undefined;
}

/** "Company — branch", or "Company — all branches" for a company-wide scope. */
export function scopeLabel(
  s: AuthSession['scopes'][number], language: string, t: (key: string) => string
): string {
  const en = language === 'en';
  const company = en ? s.companyNameEn || s.companyNameAr : s.companyNameAr;
  const branch = s.branchId === null ? t('auth.allBranches') : (en ? s.branchNameEn || s.branchNameAr : s.branchNameAr) ?? `#${s.branchId}`;
  return `${company} — ${branch}`;
}

/**
 * The sign-in screen. A user with two-factor sign-in types the code from the authenticator app (or
 * a recovery code) after the password; a user with several scopes (companies, or branches of one)
 * then picks one.
 */
export function LoginPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const setSession = useAppStore((s) => s.setSession);
  const toggleLanguage = useAppStore((s) => s.toggleLanguage);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [session, setPendingSession] = useState<AuthSession | null>(null);
  const [scopeKey, setScopeKey] = useState('');
  const [challenge, setChallenge] = useState<TwoFactorChallenge | null>(null);
  const [code, setCode] = useState('');
  const [useRecovery, setUseRecovery] = useState(false);

  const enter = (s: AuthSession) => {
    // Tabs belong to whoever opened them — a different user starts clean.
    if (useAppStore.getState().userId !== s.userId) {
      useTabsStore.setState({ tabs: [], activeTabId: null });
    }
    setSession(s);
    navigate(s.passwordChangeRequired ? '/change-password' : '/', { replace: true });
  };

  const signedIn = (result: AuthSession) => {
    if (result.scopes.length > 1 && !result.passwordChangeRequired) {
      setSession(result);
      setPendingSession(result);
      setScopeKey(`${result.companyId}:${result.branchId ?? ''}`);
    } else {
      enter(result);
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const result = await login(username.trim(), password);
      if (isTwoFactorChallenge(result)) {
        setChallenge(result);
        setCode('');
      } else {
        signedIn(result);
      }
    } catch (err) {
      setError(authErrorMessage(err, t('auth.loginFailed')));
    } finally {
      setBusy(false);
    }
  };

  const submitCode = async (e: FormEvent) => {
    e.preventDefault();
    if (!challenge) return;
    setBusy(true);
    setError(null);
    try {
      const value = code.trim();
      signedIn(await loginTwoFactor(challenge.challengeToken, useRecovery ? null : value, useRecovery ? value : null));
    } catch (err) {
      const message = authErrorMessage(err, t('auth.loginFailed'));
      // An expired challenge (or a locked account) means starting over from the password.
      if (axiosErrorCode(err) !== 'AUTH-2FA-INVALID-CODE') setChallenge(null);
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  const chooseCompany = async () => {
    if (!session || !scopeKey) return;
    const [companyPart, branchPart] = scopeKey.split(':');
    const companyId = Number(companyPart);
    const branchId = branchPart ? Number(branchPart) : null;
    setBusy(true);
    const switched = companyId === session.companyId && branchId === session.branchId ? session : await refreshSession(companyId, branchId);
    setBusy(false);
    if (switched) enter(switched);
    else setError(t('auth.loginFailed'));
  };

  const companyOptions = session ? session.scopes.map((s) => ({ value: `${s.companyId}:${s.branchId ?? ''}`, label: scopeLabel(s, i18n.language, t) })) : [];

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', background: 'var(--color-bg, #f4f6f9)', padding: 16 }}>
      <form
        onSubmit={session ? (e) => { e.preventDefault(); void chooseCompany(); } : challenge ? submitCode : submit}
        style={{ width: '100%', maxWidth: 380, display: 'flex', flexDirection: 'column', gap: 16, background: 'var(--color-surface, #fff)', padding: 28, borderRadius: 14, boxShadow: 'var(--shadow-2)', borderTop: '4px solid var(--color-gold-500)' }}
      >
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button type="button" variant="ghost" onClick={toggleLanguage}>🌐 {t('common.language')}</Button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8 }}>
          <img src="/logo.jpeg" alt="الحبّاك" style={{ width: 84, height: 84, borderRadius: 16, objectFit: 'cover' }} />
          <div style={{ fontWeight: 700, fontSize: 20, color: 'var(--color-navy-700)' }}>{t('common.appName')}</div>
          <div style={{ fontSize: 13, color: 'var(--color-gold-600)', fontWeight: 600 }}>{t('common.slogan')}</div>
        </div>

        {!session && challenge ? (
          <>
            <p style={{ margin: 0, fontSize: 13, color: 'var(--color-text-muted)', lineHeight: 1.7 }}>
              {useRecovery ? t('auth.twoFactorRecoveryHint') : t('auth.twoFactorCodeHint')}
            </p>
            <FieldWrapper label={useRecovery ? t('auth.recoveryCode') : t('auth.twoFactorCode')}>
              <Input
                id="login-2fa-code" value={code} onChange={(e) => setCode(e.target.value)} autoFocus dir="ltr"
                autoComplete="one-time-code" inputMode={useRecovery ? 'text' : 'numeric'} maxLength={useRecovery ? 20 : 6}
                style={{ letterSpacing: 4, textAlign: 'center', fontSize: 18 }}
              />
            </FieldWrapper>
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
              <Button type="button" variant="ghost" onClick={() => { setUseRecovery((v) => !v); setCode(''); setError(null); }}>
                {useRecovery ? t('auth.useAuthenticatorCode') : t('auth.useRecoveryCode')}
              </Button>
              <Button type="button" variant="ghost" onClick={() => { setChallenge(null); setError(null); }}>{t('auth.backToPassword')}</Button>
            </div>
          </>
        ) : !session ? (
          <>
            <FieldWrapper label={t('auth.username')}>
              <Input id="login-username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus dir="ltr" />
            </FieldWrapper>
            <FieldWrapper label={t('auth.password')}>
              <Input id="login-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" dir="ltr" />
            </FieldWrapper>
          </>
        ) : (
          <FieldWrapper label={t('auth.chooseCompany')}>
            <SearchableSelect id="login-company" value={scopeKey} onChange={(v) => setScopeKey(String(v))} options={companyOptions} />
          </FieldWrapper>
        )}

        {error && (
          <div role="alert" style={{ background: 'var(--color-error-bg)', color: 'var(--color-error)', padding: '10px 12px', borderRadius: 8, fontSize: 13 }}>
            {error}
          </div>
        )}

        <Button type="submit" variant="primary" disabled={busy || (!session && (challenge ? !code.trim() : !username.trim() || !password))}>
          {busy ? t('common.loading') : session || challenge ? t('auth.continue') : t('auth.login')}
        </Button>
      </form>
    </div>
  );
}
