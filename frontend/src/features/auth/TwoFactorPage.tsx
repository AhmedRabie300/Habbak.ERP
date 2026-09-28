import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import QRCode from 'qrcode';
import { Button } from '../../ui-kit/Button';
import { FieldWrapper, Input } from '../../ui-kit/Field';
import { Badge } from '../../ui-kit/Badge';
import { useAppStore } from '../../store/appStore';
import {
  authErrorMessage, useBeginTwoFactorSetup, useDisableTwoFactor, useEnableTwoFactor, useMyTwoFactor, useRegenerateRecoveryCodes,
  type TwoFactorSetup
} from './api';

const cardStyle = {
  width: '100%', maxWidth: 460, display: 'flex', flexDirection: 'column' as const, gap: 16, background: 'var(--color-surface, #fff)',
  padding: 28, borderRadius: 14, boxShadow: 'var(--shadow-2)', borderTop: '4px solid var(--color-gold-500)'
};
const hintStyle = { margin: 0, fontSize: 13, color: 'var(--color-text-muted)', lineHeight: 1.7 };
const codeInputStyle = { letterSpacing: 4, textAlign: 'center' as const, fontSize: 18 };

/**
 * The signed-in user's own two-factor sign-in: turn it on with an authenticator app (scan the QR
 * code, confirm with a code, keep the recovery codes), make new recovery codes, or turn it off.
 */
export function TwoFactorPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: status, isLoading } = useMyTwoFactor();
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [codes, setCodes] = useState<string[] | null>(null);
  const [mode, setMode] = useState<'view' | 'regenerate' | 'disable'>('view');
  const [error, setError] = useState<string | null>(null);

  const beginMutation = useBeginTwoFactorSetup();
  const begin = async () => {
    setError(null);
    try {
      setSetup(await beginMutation.mutateAsync(undefined));
    } catch (err) {
      setError(authErrorMessage(err, t('auth.twoFactorFailed')));
    }
  };

  let body: ReactNode;
  if (isLoading || !status) {
    body = <p style={hintStyle}>{t('common.loading')}</p>;
  } else if (codes) {
    body = <RecoveryCodes codes={codes} onDone={() => { setCodes(null); setMode('view'); }} />;
  } else if (setup) {
    body = <SetupStep setup={setup} onEnabled={(c) => { setSetup(null); setCodes(c); }} onCancel={() => setSetup(null)} />;
  } else if (!status.enabled) {
    body = (
      <>
        <p style={hintStyle}>{t('auth.twoFactorOffHint')}</p>
        <Button variant="primary" onClick={() => void begin()} disabled={beginMutation.isPending}>{t('auth.twoFactorTurnOn')}</Button>
      </>
    );
  } else if (mode === 'regenerate') {
    body = <RegenerateStep onDone={(c) => setCodes(c)} onCancel={() => setMode('view')} />;
  } else if (mode === 'disable') {
    body = <DisableStep onDone={() => setMode('view')} onCancel={() => setMode('view')} />;
  } else {
    body = (
      <>
        <p style={hintStyle}>{t('auth.twoFactorOnHint')}</p>
        <div style={{ fontSize: 13 }}>
          {t('auth.recoveryCodesLeft', { count: status.recoveryCodesLeft })}
          {status.recoveryCodesLeft <= 3 && <div style={{ color: 'var(--color-warning, #b7791f)', marginTop: 4 }}>{t('auth.recoveryCodesLow')}</div>}
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <Button variant="secondary" onClick={() => setMode('regenerate')}>{t('auth.newRecoveryCodes')}</Button>
          <Button variant="danger" onClick={() => setMode('disable')}>{t('auth.twoFactorTurnOff')}</Button>
        </div>
      </>
    );
  }

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', background: 'var(--color-bg, #f4f6f9)', padding: 16 }}>
      <div style={cardStyle}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
          <h2 style={{ margin: 0, fontSize: 18 }}>{t('auth.twoFactorTitle')}</h2>
          {status && <Badge label={status.enabled ? t('auth.twoFactorOn') : t('auth.twoFactorOff')} tone={status.enabled ? 'success' : 'neutral'} />}
        </div>
        {body}
        {error && <ErrorBox message={error} />}
        {!codes && !setup && mode === 'view' && (
          <Button variant="ghost" onClick={() => navigate('/', { replace: true })}>{t('auth.backToApp')}</Button>
        )}
      </div>
    </div>
  );
}

function ErrorBox({ message }: { message: string }) {
  return (
    <div role="alert" style={{ background: 'var(--color-error-bg)', color: 'var(--color-error)', padding: '10px 12px', borderRadius: 8, fontSize: 13 }}>
      {message}
    </div>
  );
}

/** Scan (or type the key), then confirm with the first code the app shows. */
function SetupStep({ setup, onEnabled, onCancel }: { setup: TwoFactorSetup; onEnabled: (codes: string[]) => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const [qr, setQr] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const enable = useEnableTwoFactor();

  useEffect(() => {
    QRCode.toDataURL(setup.setupUri, { width: 200, margin: 1 }).then(setQr).catch(() => setQr(null));
  }, [setup.setupUri]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      onEnabled(await enable.mutateAsync(code.trim()));
    } catch (err) {
      setError(authErrorMessage(err, t('auth.twoFactorFailed')));
    }
  };

  return (
    <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <p style={hintStyle}>{t('auth.twoFactorScanHint')}</p>
      <div style={{ display: 'grid', placeItems: 'center' }}>
        {qr ? <img src={qr} alt={t('auth.twoFactorQrAlt')} width={200} height={200} style={{ borderRadius: 8, background: '#fff' }} /> : <div style={{ height: 200 }} />}
      </div>
      <div style={{ fontSize: 12.5, color: 'var(--color-text-muted)' }}>
        {t('auth.twoFactorManualKey')}
        <div dir="ltr" style={{ fontFamily: 'monospace', fontSize: 14, marginTop: 4, color: 'var(--color-text)', wordBreak: 'break-all', userSelect: 'all' }}>
          {setup.secret.match(/.{1,4}/g)?.join(' ')}
        </div>
      </div>
      <FieldWrapper label={t('auth.twoFactorCode')}>
        <Input id="tf-setup-code" value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" autoComplete="one-time-code"
          maxLength={6} dir="ltr" style={codeInputStyle} autoFocus />
      </FieldWrapper>
      {error && <ErrorBox message={error} />}
      <div style={{ display: 'flex', gap: 8 }}>
        <Button type="submit" variant="primary" disabled={enable.isPending || code.trim().length !== 6}>{t('auth.twoFactorConfirm')}</Button>
        <Button type="button" variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </form>
  );
}

/** The recovery codes, shown once: copy or save them before leaving. */
function RecoveryCodes({ codes, onDone }: { codes: string[]; onDone: () => void }) {
  const { t } = useTranslation();
  const username = useAppStore((s) => s.username);
  const [saved, setSaved] = useState(false);

  const download = () => {
    const blob = new Blob([`${t('auth.recoveryCodesFileTitle', { user: username ?? '' })}\n\n${codes.join('\n')}\n`], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'habbak-recovery-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
    setSaved(true);
  };

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(codes.join('\n'));
      setSaved(true);
    } catch {
      // Clipboard refused (permissions): the codes are on screen and can be saved as a file.
    }
  };

  return (
    <>
      <p style={hintStyle}>{t('auth.recoveryCodesHint')}</p>
      <ol dir="ltr" style={{ margin: 0, padding: '12px 16px 12px 40px', background: 'var(--color-surface-2, #f7f8fa)', borderRadius: 8, columns: 2, fontFamily: 'monospace', fontSize: 14, lineHeight: 1.9 }}>
        {codes.map((c) => <li key={c}>{c}</li>)}
      </ol>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        <Button variant="secondary" onClick={download}>{t('auth.recoveryCodesDownload')}</Button>
        <Button variant="secondary" onClick={() => void copy()}>{t('auth.recoveryCodesCopy')}</Button>
        <Button variant="primary" onClick={onDone} disabled={!saved} title={saved ? undefined : t('auth.recoveryCodesSaveFirst')}>{t('auth.recoveryCodesDone')}</Button>
      </div>
    </>
  );
}

function RegenerateStep({ onDone, onCancel }: { onDone: (codes: string[]) => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const regenerate = useRegenerateRecoveryCodes();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      onDone(await regenerate.mutateAsync(code.trim()));
    } catch (err) {
      setError(authErrorMessage(err, t('auth.twoFactorFailed')));
    }
  };

  return (
    <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <p style={hintStyle}>{t('auth.newRecoveryCodesHint')}</p>
      <FieldWrapper label={t('auth.twoFactorCode')}>
        <Input id="tf-regen-code" value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" autoComplete="one-time-code"
          maxLength={6} dir="ltr" style={codeInputStyle} autoFocus />
      </FieldWrapper>
      {error && <ErrorBox message={error} />}
      <div style={{ display: 'flex', gap: 8 }}>
        <Button type="submit" variant="primary" disabled={regenerate.isPending || code.trim().length !== 6}>{t('auth.newRecoveryCodes')}</Button>
        <Button type="button" variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </form>
  );
}

function DisableStep({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const { t } = useTranslation();
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [useRecovery, setUseRecovery] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const disable = useDisableTwoFactor();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const value = code.trim();
      await disable.mutateAsync({ password, code: useRecovery ? null : value, recoveryCode: useRecovery ? value : null });
      onDone();
    } catch (err) {
      setError(authErrorMessage(err, t('auth.twoFactorFailed')));
    }
  };

  return (
    <form onSubmit={submit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <p style={hintStyle}>{t('auth.twoFactorOffConfirmHint')}</p>
      <FieldWrapper label={t('auth.currentPassword')}>
        <Input id="tf-off-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" dir="ltr" autoFocus />
      </FieldWrapper>
      <FieldWrapper label={useRecovery ? t('auth.recoveryCode') : t('auth.twoFactorCode')}>
        <Input id="tf-off-code" value={code} onChange={(e) => setCode(e.target.value)} dir="ltr" style={codeInputStyle}
          inputMode={useRecovery ? 'text' : 'numeric'} maxLength={useRecovery ? 20 : 6} autoComplete="one-time-code" />
      </FieldWrapper>
      <Button type="button" variant="ghost" onClick={() => { setUseRecovery((v) => !v); setCode(''); }}>
        {useRecovery ? t('auth.useAuthenticatorCode') : t('auth.useRecoveryCode')}
      </Button>
      {error && <ErrorBox message={error} />}
      <div style={{ display: 'flex', gap: 8 }}>
        <Button type="submit" variant="danger" disabled={disable.isPending || !password || !code.trim()}>{t('auth.twoFactorTurnOff')}</Button>
        <Button type="button" variant="ghost" onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </form>
  );
}
