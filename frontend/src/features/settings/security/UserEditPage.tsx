import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody, CardHeader } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { Button } from '../../../ui-kit/Button';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { SearchableMultiSelect } from '../../../ui-kit/SearchableMultiSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { useAppStore } from '../../../store/appStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useBranchesList } from '../../organization/branches/api';
import { useCompaniesList } from '../../organization/companies/api';
import {
  useCreateUser, useResetPassword, useRolesList, useSetUserRoles, useSetUserScopes, useResetUserTwoFactor, useUnlockUser, useUpdateUser, useUser,
  type PreferredLanguage, type UserRoleRow, type UserScopeRow, type UserStatus
} from './api';
import { Check, formatDateTime, Hint, tableStyle, tdStyle, thStyle, UserStatusBadge } from './shared';

/** /settings/users/:id — a user's profile, their roles in this company, and the companies they can enter. */
export function UserEditPage() {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const userId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const currentCompanyId = useAppStore((s) => s.companyId);
  const canEdit = usePermission(isNew ? 'add' : 'edit');

  const { data: user, isLoading } = useUser(userId);
  const { data: roles } = useRolesList();
  const { data: branches } = useBranchesList();
  const { data: companies } = useCompaniesList();

  const [username, setUsername] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [language, setLanguage] = useState<PreferredLanguage>('Arabic');
  const [status, setStatus] = useState<UserStatus>('Active');
  const [mustChange, setMustChange] = useState(true);
  const [password, setPassword] = useState('');
  const [newRoleIds, setNewRoleIds] = useState<number[]>([]);
  const [roleRows, setRoleRows] = useState<UserRoleRow[]>([]);
  const [scopeRows, setScopeRows] = useState<UserScopeRow[]>([]);
  const [resetValue, setResetValue] = useState('');
  const [resetMustChange, setResetMustChange] = useState(true);

  useEffect(() => {
    if (!user) return;
    setFullName(user.fullName);
    setEmail(user.email);
    setPhone(user.phoneNumber ?? '');
    setLanguage(user.preferredLanguage);
    setStatus(user.status);
    setMustChange(user.mustChangePassword);
    setRoleRows(user.roles.map((r) => ({ ...r })));
    setScopeRows(user.scopes.map((s) => ({ ...s })));
  }, [user]);

  const createMutation = useCreateUser();
  const updateMutation = useUpdateUser(userId ?? 0);
  const rolesMutation = useSetUserRoles(userId ?? 0);
  const scopesMutation = useSetUserScopes(userId ?? 0);
  const resetMutation = useResetPassword(userId ?? 0);
  const unlockMutation = useUnlockUser(userId ?? 0);
  const resetTwoFactorMutation = useResetUserTwoFactor(userId ?? 0);

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      showToast(success, 'success');
      return true;
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
      return false;
    }
  };

  const save = () =>
    isNew
      ? run(async () => {
          const { id: newId } = await createMutation.mutateAsync({
            username: username.trim(), email: email.trim(), fullName: fullName.trim(), phoneNumber: phone || null,
            preferredLanguage: language, password, mustChangePassword: mustChange, roleIds: newRoleIds
          });
          navigate(`/settings/users/${newId}`);
        }, t('security.userCreated'))
      : run(
          () => updateMutation.mutateAsync({
            email: email.trim(), fullName: fullName.trim(), phoneNumber: phone || null, preferredLanguage: language,
            status, mustChangePassword: mustChange, rowVersion: user!.rowVersion
          }),
          t('security.saved')
        );

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  const roleName = (code: string, fallbackAr?: string, fallbackEn?: string) =>
    t(`security.roleNames.${code}`, { defaultValue: (i18n.language === 'en' ? fallbackEn : fallbackAr) ?? code });
  const roleOptions = (roles ?? []).filter((r) => r.isActive).map((r) => ({ value: r.id, label: roleName(r.code, r.nameAr, r.nameEn) }));
  const branchOptions = [{ value: '', label: t('security.allBranches') }, ...(branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }))];
  const companyOptions = (companies ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
  const roleCodeOptions = [...new Set((roles ?? []).map((r) => r.code))].map((code) => ({ value: code, label: roleName(code) }));
  const statusOptions: UserStatus[] = ['Active', 'Suspended', 'PendingActivation'];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 980 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('security.addUser') : `${t('security.userTitle')} — ${user?.username ?? ''}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('security.createUser') : t('common.saveChanges'), onClick: () => void save(), visible: canEdit }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/users') }]}
      />

      <Card>
        <CardHeader end={user && <UserStatusBadge status={user.status} />}>{t('security.profile')}</CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('security.username')}>
              <Input id="user-username" value={isNew ? username : (user?.username ?? '')} onChange={(e) => setUsername(e.target.value)} disabled={!isNew} dir="ltr" />
            </FieldWrapper>
            <FieldWrapper label={t('security.fullName')}>
              <Input id="user-fullname" value={fullName} onChange={(e) => setFullName(e.target.value)} style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.email')}>
              <Input id="user-email" value={email} onChange={(e) => setEmail(e.target.value)} dir="ltr" style={{ minWidth: 220 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.phone')}>
              <Input id="user-phone" value={phone} onChange={(e) => setPhone(e.target.value)} dir="ltr" />
            </FieldWrapper>
            <FieldWrapper label={t('security.language')}>
              <SearchableSelect id="user-language" value={language} onChange={(v) => setLanguage(v as PreferredLanguage)}
                options={[{ value: 'Arabic', label: 'العربية' }, { value: 'English', label: 'English' }]} />
            </FieldWrapper>
            {isNew ? (
              <>
                <FieldWrapper label={t('security.password')}>
                  <Input id="user-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} dir="ltr" autoComplete="new-password" />
                </FieldWrapper>
                <FieldWrapper label={t('security.roles')}>
                  <SearchableMultiSelect value={newRoleIds} onChange={(v) => setNewRoleIds(v.map(Number))} options={roleOptions} style={{ minWidth: 260 }} />
                </FieldWrapper>
              </>
            ) : (
              <FieldWrapper label={t('security.statusLabel')}>
                <SearchableSelect id="user-status" value={status === 'Locked' ? 'Active' : status} onChange={(v) => setStatus(v as UserStatus)}
                  options={statusOptions.map((s) => ({ value: s, label: t(`security.status.${s}`) }))} disabled={user?.status === 'Locked'} />
              </FieldWrapper>
            )}
          </div>
          <div style={{ marginTop: 12 }}>
            <Check checked={mustChange} onChange={setMustChange} label={t('security.mustChangePassword')} />
          </div>
          {isNew && <div style={{ marginTop: 8 }}><Hint>{t('security.newUserHint')}</Hint></div>}

          {user && (
            <div style={{ display: 'flex', gap: 24, flexWrap: 'wrap', marginTop: 16, fontSize: 12.5, color: 'var(--color-text-muted)' }}>
              <span>{t('security.lastLogin')}: {formatDateTime(user.lastLoginAtUtc, i18n.language)}</span>
              <span>{t('security.passwordChangedAt')}: {formatDateTime(user.passwordChangedAtUtc, i18n.language)}</span>
              <span>{t('security.failedAttempts')}: {user.failedLoginAttempts}</span>
              <span style={{ display: 'inline-flex', gap: 8, alignItems: 'center' }}>
                {t('auth.twoFactorTitle')}: {user.twoFactorEnabled ? t('auth.twoFactorOn') : t('auth.twoFactorOff')}
                {user.twoFactorEnabled && canEdit && (
                  <Button size="sm" variant="secondary"
                    onClick={() => { if (window.confirm(t('security.resetTwoFactorConfirm'))) void run(() => resetTwoFactorMutation.mutateAsync(), t('security.twoFactorReset')); }}>
                    {t('security.resetTwoFactor')}
                  </Button>
                )}
              </span>
              {user.status === 'Locked' && (
                <span style={{ display: 'inline-flex', gap: 8, alignItems: 'center', color: 'var(--color-error)' }}>
                  {t('security.lockedUntil')}: {formatDateTime(user.lockedUntilUtc, i18n.language)}
                  {canEdit && <Button size="sm" variant="secondary" onClick={() => void run(() => unlockMutation.mutateAsync(), t('security.unlocked'))}>{t('security.unlock')}</Button>}
                </span>
              )}
            </div>
          )}
        </CardBody>
      </Card>

      {user && (
        <>
          <Card>
            <CardHeader end={canEdit && (
              <Button size="sm" variant="primary" disabled={rolesMutation.isPending}
                onClick={() => void run(() => rolesMutation.mutateAsync(roleRows.filter((r) => r.roleId).map((r) => ({ roleId: r.roleId, branchId: r.branchId, expiresAtUtc: r.expiresAtUtc }))), t('security.rolesSaved'))}>
                {t('security.saveRoles')}
              </Button>
            )}>
              {t('security.rolesInCompany')}
            </CardHeader>
            <CardBody>
              <Hint>{t('security.rolesHint')}</Hint>
              <div style={{ overflowX: 'auto', marginTop: 10 }}>
                <table style={tableStyle}>
                  <thead><tr><th style={thStyle}>{t('security.role')}</th><th style={thStyle}>{t('security.branch')}</th><th style={thStyle}>{t('security.expiresAt')}</th><th style={thStyle} /></tr></thead>
                  <tbody>
                    {roleRows.map((row, i) => (
                      <tr key={i}>
                        <td style={tdStyle}>
                          <SearchableSelect value={row.roleId || ''} onChange={(v) => setRoleRows(roleRows.map((r, j) => (j === i ? { ...r, roleId: Number(v) } : r)))} options={roleOptions} disabled={!canEdit} style={{ minWidth: 200 }} />
                        </td>
                        <td style={tdStyle}>
                          <SearchableSelect value={row.branchId ?? ''} onChange={(v) => setRoleRows(roleRows.map((r, j) => (j === i ? { ...r, branchId: v === '' ? null : Number(v) } : r)))} options={branchOptions} disabled={!canEdit} style={{ minWidth: 180 }} />
                        </td>
                        <td style={tdStyle}>
                          <Input type="date" value={row.expiresAtUtc?.slice(0, 10) ?? ''} disabled={!canEdit}
                            onChange={(e) => setRoleRows(roleRows.map((r, j) => (j === i ? { ...r, expiresAtUtc: e.target.value ? `${e.target.value}T23:59:59Z` : null } : r)))} />
                        </td>
                        <td style={tdStyle}>
                          {canEdit && <Button size="sm" variant="ghost" onClick={() => setRoleRows(roleRows.filter((_, j) => j !== i))}>✕</Button>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {canEdit && <Button size="sm" variant="ghost" style={{ marginTop: 8 }} onClick={() => setRoleRows([...roleRows, { roleId: 0, branchId: null, expiresAtUtc: null }])}>+ {t('security.addRole')}</Button>}
            </CardBody>
          </Card>

          <Card>
            <CardHeader end={canEdit && (
              <Button size="sm" variant="primary" disabled={scopesMutation.isPending}
                onClick={() => void run(() => scopesMutation.mutateAsync(scopeRows.map(({ id: _id, ...s }) => s)), t('security.scopesSaved'))}>
                {t('security.saveScopes')}
              </Button>
            )}>
              {t('security.scopesTitle')}
            </CardHeader>
            <CardBody>
              <Hint>{t('security.scopesHint')}</Hint>
              <div style={{ overflowX: 'auto', marginTop: 10 }}>
                <table style={tableStyle}>
                  <thead>
                    <tr>
                      <th style={thStyle}>{t('security.company')}</th><th style={thStyle}>{t('security.branch')}</th><th style={thStyle}>{t('security.roleInScope')}</th>
                      <th style={thStyle}>{t('security.defaultScope')}</th><th style={thStyle}>{t('security.active')}</th><th style={thStyle} />
                    </tr>
                  </thead>
                  <tbody>
                    {scopeRows.map((row, i) => (
                      <tr key={i}>
                        <td style={tdStyle}>
                          <SearchableSelect value={row.companyId || ''} onChange={(v) => setScopeRows(scopeRows.map((s, j) => (j === i ? { ...s, companyId: Number(v) } : s)))} options={companyOptions} disabled={!canEdit} style={{ minWidth: 180 }} />
                        </td>
                        <td style={tdStyle}>
                          <SearchableSelect value={row.branchId ?? ''} onChange={(v) => setScopeRows(scopeRows.map((s, j) => (j === i ? { ...s, branchId: v === '' ? null : Number(v) } : s)))}
                            options={row.companyId === currentCompanyId ? branchOptions : [branchOptions[0]]} disabled={!canEdit} style={{ minWidth: 160 }} />
                        </td>
                        <td style={tdStyle}>
                          <SearchableSelect value={row.roleInScope} onChange={(v) => setScopeRows(scopeRows.map((s, j) => (j === i ? { ...s, roleInScope: String(v) } : s)))} options={roleCodeOptions} disabled={!canEdit} style={{ minWidth: 170 }} />
                        </td>
                        <td style={tdStyle}>
                          <input type="radio" name="default-scope" checked={row.isDefault} disabled={!canEdit}
                            onChange={() => setScopeRows(scopeRows.map((s, j) => ({ ...s, isDefault: j === i })))} />
                        </td>
                        <td style={tdStyle}>
                          <Check checked={row.isActive} disabled={!canEdit} onChange={(v) => setScopeRows(scopeRows.map((s, j) => (j === i ? { ...s, isActive: v } : s)))} />
                        </td>
                        <td style={tdStyle}>
                          {canEdit && <Button size="sm" variant="ghost" onClick={() => setScopeRows(scopeRows.filter((_, j) => j !== i))}>✕</Button>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {canEdit && (
                <Button size="sm" variant="ghost" style={{ marginTop: 8 }}
                  onClick={() => setScopeRows([...scopeRows, { companyId: currentCompanyId ?? 0, branchId: null, roleInScope: roleCodeOptions[0]?.value ?? '', isDefault: scopeRows.length === 0, isActive: true }])}>
                  + {t('security.addScope')}
                </Button>
              )}
            </CardBody>
          </Card>

          {canEdit && (
            <Card>
              <CardHeader>{t('security.resetPassword')}</CardHeader>
              <CardBody>
                <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
                  <FieldWrapper label={t('security.newPassword')}>
                    <Input id="user-reset" type="password" value={resetValue} onChange={(e) => setResetValue(e.target.value)} dir="ltr" autoComplete="new-password" />
                  </FieldWrapper>
                  <div style={{ paddingBottom: 10 }}><Check checked={resetMustChange} onChange={setResetMustChange} label={t('security.mustChangePassword')} /></div>
                  <Button variant="secondary" disabled={!resetValue || resetMutation.isPending}
                    onClick={async () => {
                      if (await run(() => resetMutation.mutateAsync({ newPassword: resetValue, mustChangePassword: resetMustChange }), t('security.passwordReset'))) setResetValue('');
                    }}>
                    {t('security.resetPassword')}
                  </Button>
                </div>
                <div style={{ marginTop: 8 }}><Hint>{t('security.resetHint')}</Hint></div>
              </CardBody>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
