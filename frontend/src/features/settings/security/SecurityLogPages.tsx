import { useEffect, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody, CardHeader } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { Button } from '../../../ui-kit/Button';
import { Badge } from '../../../ui-kit/Badge';
import { ConfirmModal } from '../../../ui-kit/Modal';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import {
  useActiveSessions, useAuditEntityTypes, useAuditLog, useLoginAttempts, useRevokeSession, useSecuritySettings, useUpdateSecuritySettings, useUsersList,
  type ActiveSession, type AuditActionType, type AuditFilters, type LoginAttemptFilters, type SecuritySettings
} from './api';
import { Check, formatDateTime, Hint, PageHeader, tableStyle, tdStyle, thStyle } from './shared';

function Table({ head, children, empty }: { head: ReactNode[]; children: ReactNode; empty: boolean }) {
  const { t } = useTranslation();
  return (
    <div style={{ overflowX: 'auto', background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', borderTop: '3px solid var(--color-gold-500)' }}>
      <table style={tableStyle}>
        <thead><tr>{head.map((h, i) => <th key={i} style={thStyle}>{h}</th>)}</tr></thead>
        <tbody>
          {empty ? <tr><td style={{ ...tdStyle, textAlign: 'center', color: 'var(--color-text-muted)' }} colSpan={head.length}>{t('common.noData')}</td></tr> : children}
        </tbody>
      </table>
    </div>
  );
}

function Pager({ page, total, pageSize, onPage }: { page: number; total: number; pageSize: number; onPage: (p: number) => void }) {
  const { t } = useTranslation();
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'flex-end', fontSize: 13 }}>
      <span style={{ color: 'var(--color-text-muted)' }}>{t('security.pageOf', { page, pages, total })}</span>
      <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>‹</Button>
      <Button size="sm" variant="secondary" disabled={page >= pages} onClick={() => onPage(page + 1)}>›</Button>
    </div>
  );
}

const toUtc = (date: string, endOfDay = false) => (date ? `${date}T${endOfDay ? '23:59:59' : '00:00:00'}` : '');

// ---------------------------------------------------------------------------------- sessions

/** /settings/sessions — who is signed in right now, and ending a session. */
export function SessionsPage() {
  const { t, i18n } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const canRevoke = usePermission('delete');
  const { data, isLoading } = useActiveSessions();
  const revoke = useRevokeSession();
  const [pending, setPending] = useState<ActiveSession | null>(null);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader title={t('security.sessionsTitle')} />
      <Hint>{t('security.sessionsHint')}</Hint>
      {isLoading ? <div>{t('common.loading')}</div> : (
        <Table empty={!data?.length} head={[t('security.username'), t('security.fullName'), t('security.signedInAt'), t('security.expiresAt'), t('security.ip'), t('security.device'), '']}>
          {data?.map((s) => (
            <tr key={s.id}>
              <td style={tdStyle} dir="ltr">{s.username}</td>
              <td style={tdStyle}>{s.fullName}</td>
              <td style={tdStyle}>{formatDateTime(s.createdAtUtc, i18n.language)}</td>
              <td style={tdStyle}>{formatDateTime(s.expiresAtUtc, i18n.language)}</td>
              <td style={tdStyle} dir="ltr">{s.createdByIp}</td>
              <td style={{ ...tdStyle, maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={s.userAgent ?? ''}>{s.userAgent ?? '—'}</td>
              <td style={tdStyle}>{canRevoke && <Button size="sm" variant="danger" onClick={() => setPending(s)}>{t('security.endSession')}</Button>}</td>
            </tr>
          ))}
        </Table>
      )}
      <ConfirmModal
        open={pending !== null}
        title={t('security.endSession')}
        message={t('security.endSessionConfirm', { user: pending?.fullName ?? '' })}
        onCancel={() => setPending(null)}
        onConfirm={async () => {
          const target = pending!;
          setPending(null);
          try {
            await revoke.mutateAsync(target.id);
            showToast(t('security.sessionEnded'), 'success');
          } catch (error) {
            const message = getFieldErrorMessage(error);
            if (message) showToast(message, 'error');
          }
        }}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------- login attempts

/** /settings/login-attempts — every sign-in attempt, failed ones first to catch the eye. */
export function LoginAttemptsPage() {
  const { t, i18n } = useTranslation();
  const [f, setF] = useState<LoginAttemptFilters>({ username: '', success: '', fromUtc: '', toUtc: '', page: 1 });
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data, isLoading } = useLoginAttempts(f);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader title={t('security.loginAttemptsTitle')} />
      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('security.username')}>
              <Input id="la-username" value={f.username} dir="ltr" onChange={(e) => setF({ ...f, username: e.target.value, page: 1 })} />
            </FieldWrapper>
            <FieldWrapper label={t('security.result')}>
              <SearchableSelect id="la-success" value={f.success} onChange={(v) => setF({ ...f, success: v as LoginAttemptFilters['success'], page: 1 })}
                options={[{ value: '', label: t('security.all') }, { value: 'true', label: t('security.succeeded') }, { value: 'false', label: t('security.failed') }]} style={{ minWidth: 140 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.from')}>
              <Input id="la-from" type="date" value={from} onChange={(e) => { setFrom(e.target.value); setF({ ...f, fromUtc: toUtc(e.target.value), page: 1 }); }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.to')}>
              <Input id="la-to" type="date" value={to} onChange={(e) => { setTo(e.target.value); setF({ ...f, toUtc: toUtc(e.target.value, true), page: 1 }); }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
      {isLoading ? <div>{t('common.loading')}</div> : (
        <>
          <Table empty={!data?.items.length} head={[t('security.when'), t('security.username'), t('security.result'), t('security.reason'), t('security.ip'), t('security.device')]}>
            {data?.items.map((a) => (
              <tr key={a.id}>
                <td style={tdStyle}>{formatDateTime(a.attemptedAtUtc, i18n.language)}</td>
                <td style={tdStyle} dir="ltr">{a.username}</td>
                <td style={tdStyle}><Badge label={a.success ? t('security.succeeded') : t('security.failed')} tone={a.success ? 'success' : 'error'} /></td>
                <td style={tdStyle}>{a.failureReason ? t(`security.failureReasons.${a.failureReason}`, { defaultValue: a.failureReason }) : '—'}</td>
                <td style={tdStyle} dir="ltr">{a.ipAddress}</td>
                <td style={{ ...tdStyle, maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={a.userAgent ?? ''}>{a.userAgent ?? '—'}</td>
              </tr>
            ))}
          </Table>
          {data && <Pager page={data.page} total={data.totalCount} pageSize={data.pageSize} onPage={(page) => setF({ ...f, page })} />}
        </>
      )}
    </div>
  );
}

// --------------------------------------------------------------------------------- audit log

const ACTIONS: AuditActionType[] = ['Create', 'Update', 'Delete', 'Approve', 'Reject', 'Login', 'Logout'];

/** /settings/audit-log — who changed what, field by field. Sensitive values are stored as [REDACTED]. */
export function AuditLogPage() {
  const { t, i18n } = useTranslation();
  const [f, setF] = useState<AuditFilters>({ userId: '', entityType: '', entityId: '', actionType: '', fromUtc: '', toUtc: '', page: 1 });
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const { data, isLoading } = useAuditLog(f);
  const { data: entityTypes } = useAuditEntityTypes();
  const { data: users } = useUsersList('', '');
  const tone = (a: AuditActionType) => (a === 'Delete' || a === 'Reject' ? 'error' : a === 'Create' || a === 'Approve' ? 'success' : a === 'Update' ? 'info' : 'neutral');
  const value = (v: string | null) => (v === '[REDACTED]' ? <span style={{ color: 'var(--color-text-muted)' }}>🔒 {t('security.hiddenValue')}</span> : v ?? '—');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader title={t('security.auditLogTitle')} />
      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('security.user')}>
              <SearchableSelect id="al-user" value={f.userId} onChange={(v) => setF({ ...f, userId: String(v), page: 1 })} style={{ minWidth: 180 }}
                options={[{ value: '', label: t('security.all') }, { value: '0', label: t('security.systemUser') }, ...(users ?? []).map((u) => ({ value: String(u.id), label: u.fullName }))]} />
            </FieldWrapper>
            <FieldWrapper label={t('security.entity')}>
              <SearchableSelect id="al-entity" value={f.entityType} onChange={(v) => setF({ ...f, entityType: String(v), page: 1 })} style={{ minWidth: 180 }}
                options={[{ value: '', label: t('security.all') }, ...(entityTypes ?? []).map((e) => ({ value: e, label: e }))]} />
            </FieldWrapper>
            <FieldWrapper label={t('security.recordId')}>
              <Input id="al-entity-id" value={f.entityId} dir="ltr" onChange={(e) => setF({ ...f, entityId: e.target.value.replace(/\D/g, ''), page: 1 })} style={{ width: 110 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.action')}>
              <SearchableSelect id="al-action" value={f.actionType} onChange={(v) => setF({ ...f, actionType: v as AuditActionType | '', page: 1 })} style={{ minWidth: 140 }}
                options={[{ value: '', label: t('security.all') }, ...ACTIONS.map((a) => ({ value: a, label: t(`security.actions.${a}`) }))]} />
            </FieldWrapper>
            <FieldWrapper label={t('security.from')}>
              <Input id="al-from" type="date" value={from} onChange={(e) => { setFrom(e.target.value); setF({ ...f, fromUtc: toUtc(e.target.value), page: 1 }); }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.to')}>
              <Input id="al-to" type="date" value={to} onChange={(e) => { setTo(e.target.value); setF({ ...f, toUtc: toUtc(e.target.value, true), page: 1 }); }} />
            </FieldWrapper>
          </div>
        </CardBody>
      </Card>
      {isLoading ? <div>{t('common.loading')}</div> : (
        <>
          <Table empty={!data?.items.length} head={[t('security.when'), t('security.user'), t('security.action'), t('security.entity'), t('security.recordId'), t('security.field'), t('security.oldValue'), t('security.newValue')]}>
            {data?.items.map((a) => (
              <tr key={a.id}>
                <td style={{ ...tdStyle, whiteSpace: 'nowrap' }}>{formatDateTime(a.occurredAtUtc, i18n.language)}</td>
                <td style={tdStyle}>{a.userId === 0 ? t('security.systemUser') : a.username ?? `#${a.userId}`}</td>
                <td style={tdStyle}><Badge label={t(`security.actions.${a.actionType}`)} tone={tone(a.actionType)} /></td>
                <td style={tdStyle} dir="ltr">{a.entityType}</td>
                <td style={tdStyle}>{a.entityId ?? '—'}</td>
                <td style={tdStyle} dir="ltr">{a.fieldName ?? '—'}</td>
                <td style={{ ...tdStyle, maxWidth: 220, wordBreak: 'break-word' }}>{value(a.oldValue)}</td>
                <td style={{ ...tdStyle, maxWidth: 220, wordBreak: 'break-word' }}>{value(a.newValue)}</td>
              </tr>
            ))}
          </Table>
          {data && <Pager page={data.page} total={data.totalCount} pageSize={data.pageSize} onPage={(page) => setF({ ...f, page })} />}
        </>
      )}
    </div>
  );
}

// -------------------------------------------------------------------------- security settings

/** /settings/security — this company's password, lockout and session policy. */
export function SecuritySettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const canEdit = usePermission('edit');
  const { data, isLoading } = useSecuritySettings();
  const update = useUpdateSecuritySettings();
  const [s, setS] = useState<SecuritySettings | null>(null);

  useEffect(() => {
    if (data) setS(data);
  }, [data]);

  if (isLoading || !s) return <div>{t('common.loading')}</div>;

  const num = (key: keyof SecuritySettings, label: string, hint?: string) => (
    <FieldWrapper label={label}>
      <Input id={`sec-${key}`} type="number" value={String(s[key] ?? '')} disabled={!canEdit} style={{ width: 120 }}
        onChange={(e) => setS({ ...s, [key]: Number(e.target.value) })} />
      {hint && <div style={{ fontSize: 11.5, color: 'var(--color-text-muted)', marginTop: 4 }}>{hint}</div>}
    </FieldWrapper>
  );

  const save = async () => {
    try {
      await update.mutateAsync(s);
      showToast(t('security.saved'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 860 }}>
      <h2 style={{ margin: 0 }}>{t('security.securityTitle')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: () => void save(), visible: canEdit }} />

      <Card>
        <CardHeader>{t('security.passwordPolicy')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {num('passwordMinLength', t('security.minLength'))}
            {num('passwordExpiryDays', t('security.expiryDays'), t('security.expiryDaysHint'))}
          </div>
          <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', marginTop: 12 }}>
            <Check checked={s.passwordRequireUppercase} disabled={!canEdit} onChange={(v) => setS({ ...s, passwordRequireUppercase: v })} label={t('security.requireUpper')} />
            <Check checked={s.passwordRequireLowercase} disabled={!canEdit} onChange={(v) => setS({ ...s, passwordRequireLowercase: v })} label={t('security.requireLower')} />
            <Check checked={s.passwordRequireDigit} disabled={!canEdit} onChange={(v) => setS({ ...s, passwordRequireDigit: v })} label={t('security.requireDigit')} />
            <Check checked={s.passwordRequireSpecial} disabled={!canEdit} onChange={(v) => setS({ ...s, passwordRequireSpecial: v })} label={t('security.requireSpecial')} />
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('security.loginPolicy')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {num('maxFailedLoginAttempts', t('security.maxAttempts'))}
            {num('accountLockoutMinutes', t('security.lockoutMinutes'))}
            {num('sessionTimeoutMinutes', t('security.sessionMinutes'), t('security.sessionMinutesHint'))}
            {num('refreshTokenExpiryDays', t('security.refreshDays'), t('security.refreshDaysHint'))}
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardHeader>{t('security.auditPolicy')}</CardHeader>
        <CardBody>
          {num('auditRetentionYears', t('security.retentionYears'))}
          <div style={{ marginTop: 8 }}><Hint>{t('security.retentionHint')}</Hint></div>
        </CardBody>
      </Card>
    </div>
  );
}
