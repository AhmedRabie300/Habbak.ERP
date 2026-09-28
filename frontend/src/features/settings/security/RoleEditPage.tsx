import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody, CardHeader } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { Button } from '../../../ui-kit/Button';
import { Badge } from '../../../ui-kit/Badge';
import { usePermission } from '../../../ui-kit/usePermission';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import {
  useCreateRole, useDeleteRole, usePermissionCatalog, useRole, useRoleButtons, useSetRoleButtons, useSetRoleFields, useSetRoleScreens, useUpdateRole,
  type ButtonPermissionRow, type FieldPermissionRow, type PermissionCatalog, type RoleDetail, type ScreenPermissionRow
} from './api';
import { Check, Hint, tableStyle, tdStyle, thStyle } from './shared';

const RIGHTS = ['canView', 'canAdd', 'canEdit', 'canDelete', 'canPrint', 'canExport', 'canApprove'] as const;
type Right = (typeof RIGHTS)[number];

const emptyRow = (screenCode: string): ScreenPermissionRow => ({
  screenCode, canView: false, canAdd: false, canEdit: false, canDelete: false, canPrint: false, canExport: false, canApprove: false
});

type Tab = 'screens' | 'fields' | 'buttons';

/**
 * /settings/roles/:id — a role and, in three tabs, what it may do on each screen, which sensitive
 * fields it may see or change on each screen, and which special buttons it may press. Full-access roles (SUPER_ADMIN, COMPANY_ADMIN) skip every check, so there is
 * nothing to tick for them.
 */
export function RoleEditPage() {
  const { t, i18n } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const roleId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const canEdit = usePermission(isNew ? 'add' : 'edit');
  const canDelete = usePermission('delete');
  const en = i18n.language === 'en';

  const { data: role, isLoading } = useRole(roleId);
  const { data: catalog } = usePermissionCatalog();

  const [code, setCode] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [description, setDescription] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [screens, setScreens] = useState<Record<string, ScreenPermissionRow>>({});
  const [filter, setFilter] = useState('');
  const [tab, setTab] = useState<Tab>('screens');

  useEffect(() => {
    if (!role) return;
    setNameAr(role.nameAr);
    setNameEn(role.nameEn);
    setDescription(role.description ?? '');
    setIsActive(role.isActive);
    setScreens(Object.fromEntries(role.screens.map((s) => [s.screenCode, s])));
  }, [role]);

  const createMutation = useCreateRole();
  const updateMutation = useUpdateRole(roleId ?? 0);
  const deleteMutation = useDeleteRole();
  const screensMutation = useSetRoleScreens(roleId ?? 0);

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      showToast(success, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const groups = useMemo(() => {
    const q = filter.trim().toLowerCase();
    const map = new Map<string, NonNullable<typeof catalog>['screens']>();
    for (const s of catalog?.screens ?? []) {
      if (q && !s.nameAr.includes(q) && !s.nameEn.toLowerCase().includes(q) && !s.code.toLowerCase().includes(q)) continue;
      const g = en ? s.groupNameEn : s.groupNameAr;
      map.set(g, [...(map.get(g) ?? []), s]);
    }
    return [...map.entries()];
  }, [catalog, filter, en]);

  const setRight = (screenCode: string, right: Right, value: boolean) =>
    setScreens((prev) => {
      const row = { ...(prev[screenCode] ?? emptyRow(screenCode)), [right]: value };
      // Anything beyond looking needs looking; taking away looking takes away the rest.
      if (right === 'canView' && !value) RIGHTS.forEach((r) => { row[r] = false; });
      if (right !== 'canView' && value) row.canView = true;
      return { ...prev, [screenCode]: row };
    });

  const setRowAll = (screenCode: string, value: boolean) =>
    setScreens((prev) => ({ ...prev, [screenCode]: { ...emptyRow(screenCode), ...Object.fromEntries(RIGHTS.map((r) => [r, value])) } }));

  const setColumn = (codes: string[], right: Right, value: boolean) => codes.forEach((c) => setRight(c, right, value));

  if (!isNew && isLoading) return <div>{t('common.loading')}</div>;

  const locked = role?.isSystemRole ?? false;
  const fullAccess = role?.isFullAccess ?? false;
  const matrixEditable = canEdit && !fullAccess && !isNew;

  const save = () =>
    isNew
      ? run(async () => {
          const { id: newId } = await createMutation.mutateAsync({ code: code.trim().toUpperCase(), nameAr, nameEn, description: description || null, isActive });
          navigate(`/settings/roles/${newId}`);
        }, t('security.roleCreated'))
      : run(() => updateMutation.mutateAsync({ nameAr, nameEn, description: description || null, isActive, rowVersion: role!.rowVersion }), t('security.saved'));

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{isNew ? t('security.addRoleButton') : `${t('security.roleTitle')} — ${en ? role?.nameEn : role?.nameAr}`}</h2>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('security.createRole') : t('common.saveChanges'), onClick: () => void save(), visible: canEdit && !locked }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/settings/roles') }]}
        destructive={!isNew && !locked && canDelete ? [{
          key: 'delete', label: t('common.remove'), confirmMessage: t('security.deleteRoleConfirm'),
          onClick: () => void run(async () => { await deleteMutation.mutateAsync(roleId!); navigate('/settings/roles'); }, t('security.roleDeleted'))
        }] : []}
      />

      <Card>
        <CardHeader end={<span style={{ display: 'inline-flex', gap: 6 }}>
          {locked && <Badge label={t('security.systemRole')} tone="info" />}
          {fullAccess && <Badge label={t('security.fullAccess')} tone="success" />}
        </span>}>
          {t('security.roleInfo')}
        </CardHeader>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('security.code')}>
              <Input id="role-code" value={isNew ? code : (role?.code ?? '')} onChange={(e) => setCode(e.target.value)} disabled={!isNew} dir="ltr" />
            </FieldWrapper>
            <FieldWrapper label={t('security.nameAr')}>
              <Input id="role-name-ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} disabled={locked} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.nameEn')}>
              <Input id="role-name-en" value={nameEn} onChange={(e) => setNameEn(e.target.value)} disabled={locked} style={{ minWidth: 200 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.description')}>
              <Input id="role-description" value={description} onChange={(e) => setDescription(e.target.value)} disabled={locked} style={{ minWidth: 280 }} />
            </FieldWrapper>
            <FieldWrapper label={t('security.active')}>
              <div style={{ height: 38, display: 'flex', alignItems: 'center' }}><Check checked={isActive} onChange={setIsActive} disabled={locked} /></div>
            </FieldWrapper>
          </div>
          {locked && <div style={{ marginTop: 8 }}><Hint>{t('security.systemRoleHint')}</Hint></div>}
          {isNew && <div style={{ marginTop: 8 }}><Hint>{t('security.newRoleHint')}</Hint></div>}
        </CardBody>
      </Card>

      {!isNew && (
        fullAccess ? (
          <Card><CardBody><Hint>{t('security.fullAccessHint')}</Hint></CardBody></Card>
        ) : (
          <>
            <div role="tablist" style={{ display: 'flex', gap: 4, borderBottom: '1px solid var(--color-border)' }}>
              {(['screens', 'fields', 'buttons'] as const).map((key) => (
                <button
                  key={key} role="tab" aria-selected={tab === key} onClick={() => setTab(key)}
                  style={{
                    padding: '8px 16px', background: 'transparent', border: 'none', cursor: 'pointer', fontWeight: tab === key ? 700 : 500,
                    color: tab === key ? 'var(--color-text)' : 'var(--color-text-muted)',
                    borderBottom: tab === key ? '3px solid var(--color-gold-500)' : '3px solid transparent', marginBottom: -1
                  }}
                >
                  {t(`security.tab${key[0].toUpperCase()}${key.slice(1)}`)}
                </button>
              ))}
            </div>

            {tab === 'screens' && (
              <Card>
                <CardHeader end={matrixEditable && (
                  <Button size="sm" variant="primary" disabled={screensMutation.isPending}
                    onClick={() => void run(() => screensMutation.mutateAsync(Object.values(screens)), t('security.screensSaved'))}>
                    {t('security.saveScreens')}
                  </Button>
                )}>
                  {t('security.screenPermissions')}
                </CardHeader>
                <CardBody>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', alignItems: 'center', marginBottom: 10 }}>
                    <Hint>{t('security.screensHint')}</Hint>
                    <Input id="role-screen-filter" placeholder={t('common.search')} value={filter} onChange={(e) => setFilter(e.target.value)} style={{ maxWidth: 240 }} />
                  </div>
                  <div style={{ overflowX: 'auto' }}>
                    <table style={tableStyle}>
                      <thead>
                        <tr>
                          <th style={thStyle}>{t('security.screen')}</th>
                          {RIGHTS.map((r) => <th key={r} style={{ ...thStyle, textAlign: 'center' }}>{t(`security.rights.${r}`)}</th>)}
                          <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.all')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {groups.map(([group, items]) => (
                          <GroupRows
                            key={group}
                            group={group}
                            items={items.map((s) => ({ code: s.code, name: en ? s.nameEn : s.nameAr }))}
                            screens={screens}
                            editable={matrixEditable}
                            onRight={setRight}
                            onRow={setRowAll}
                            onColumn={setColumn}
                          />
                        ))}
                      </tbody>
                    </table>
                  </div>
                </CardBody>
              </Card>
  
            )}
            {tab === 'fields' && role && catalog && (
              <FieldsTab role={role} catalog={catalog} editable={matrixEditable} en={en} run={run} />
            )}
            {tab === 'buttons' && role && catalog && (
              <ButtonsTab role={role} catalog={catalog} editable={matrixEditable} en={en} run={run} />
            )}
          </>
        )
      )}
    </div>
  );
}

type Run = (action: () => Promise<unknown>, success: string) => Promise<void>;

function ScreenPicker({ value, onChange, options }: { value: string; onChange: (code: string) => void; options: { value: string; label: string }[] }) {
  const { t } = useTranslation();
  return (
    <FieldWrapper label={t('security.pickScreen')}>
      <SearchableSelect id="role-rule-screen" value={value} onChange={onChange} options={options} style={{ minWidth: 260 }} />
    </FieldWrapper>
  );
}

/** The sensitive fields of one screen: the same field may be open on one screen and hidden on another. */
function FieldsTab({ role, catalog, editable, en, run }: { role: RoleDetail; catalog: PermissionCatalog; editable: boolean; en: boolean; run: Run }) {
  const { t } = useTranslation();
  const [screen, setScreen] = useState(catalog.fieldScreens[0]?.screenCode ?? '');
  const [fields, setFields] = useState<Record<string, FieldPermissionRow>>({});
  const mutation = useSetRoleFields(role.id);

  // The role's saved rules for the chosen screen, keyed entity.field.
  useEffect(() => {
    setFields(Object.fromEntries(role.fields.filter((f) => f.screenCode === screen).map((f) => [`${f.entityType}.${f.fieldName}`, f])));
  }, [role, screen]);

  const current = catalog.fieldScreens.find((s) => s.screenCode === screen);
  return (
    <Card>
      <CardHeader end={editable && current && (
        <Button size="sm" variant="primary" disabled={mutation.isPending}
          onClick={() => void run(() => mutation.mutateAsync({ screenCode: screen, rows: Object.values(fields) }), t('security.fieldsSaved'))}>
          {t('security.saveFields')}
        </Button>
      )}>
        {t('security.fieldPermissions')}
      </CardHeader>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'end', marginBottom: 10 }}>
          <ScreenPicker value={screen} onChange={setScreen}
            options={catalog.fieldScreens.map((s) => ({ value: s.screenCode, label: en ? s.nameEn : s.nameAr }))} />
          <Hint>{t('security.fieldsScreenHint')}</Hint>
        </div>
        <Hint>{t('security.fieldsHint')}</Hint>
        <div style={{ overflowX: 'auto', marginTop: 10 }}>
          <table style={tableStyle}>
            <thead>
              <tr>
                <th style={thStyle}>{t('security.field')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.restrict')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.rights.canView')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.rights.canEdit')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.auditChanges')}</th>
              </tr>
            </thead>
            <tbody>
              {(current?.fields ?? []).map((f) => {
                const key = `${f.entityType}.${f.fieldName}`;
                const row = fields[key];
                const update = (patch: Partial<FieldPermissionRow> | null) =>
                  setFields((prev) => {
                    const next = { ...prev };
                    if (patch === null) delete next[key];
                    else next[key] = {
                      ...(prev[key] ?? { screenCode: screen, entityType: f.entityType, fieldName: f.fieldName, canView: true, canEdit: true, requiresAuditLog: true }),
                      ...patch
                    };
                    return next;
                  });
                return (
                  <tr key={key}>
                    <td style={tdStyle}>
                      {t(`security.entities.${f.entityType}`, { defaultValue: f.entityType })} — {t(`security.fields.${f.fieldName}`, { defaultValue: f.fieldName })}
                    </td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={!!row} disabled={!editable} onChange={(v) => update(v ? { canView: false, canEdit: false } : null)} /></td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={row ? row.canView : true} disabled={!editable || !row} onChange={(v) => update({ canView: v, canEdit: v && (row?.canEdit ?? false) })} /></td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={row ? row.canEdit : true} disabled={!editable || !row || !row.canView} onChange={(v) => update({ canEdit: v })} /></td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={row ? row.requiresAuditLog : true} disabled={!editable || !row} onChange={(v) => update({ requiresAuditLog: v })} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </CardBody>
    </Card>
  );
}

/**
 * The special buttons of one screen. Without a custom rule a button follows the screen permission
 * it falls back on — shown as it stands for this role; a custom rule decides on its own.
 */
function ButtonsTab({ role, catalog, editable, en, run }: { role: RoleDetail; catalog: PermissionCatalog; editable: boolean; en: boolean; run: Run }) {
  const { t } = useTranslation();
  const [screen, setScreen] = useState(catalog.buttonScreens[0]?.screenCode ?? '');
  const [rules, setRules] = useState<Record<string, ButtonPermissionRow>>({});
  const { data: effective } = useRoleButtons(role.id, screen);
  const mutation = useSetRoleButtons(role.id);

  useEffect(() => {
    setRules(Object.fromEntries(role.buttons.filter((b) => b.screenCode === screen).map((b) => [b.buttonCode, b])));
  }, [role, screen]);

  const current = catalog.buttonScreens.find((s) => s.screenCode === screen);
  return (
    <Card>
      <CardHeader end={editable && current && (
        <Button size="sm" variant="primary" disabled={mutation.isPending}
          onClick={() => void run(() => mutation.mutateAsync({ screenCode: screen, rows: Object.values(rules) }), t('security.buttonsSaved'))}>
          {t('security.saveButtons')}
        </Button>
      )}>
        {t('security.buttonPermissions')}
      </CardHeader>
      <CardBody>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'end', marginBottom: 10 }}>
          <ScreenPicker value={screen} onChange={setScreen}
            options={catalog.buttonScreens.map((s) => ({ value: s.screenCode, label: en ? s.nameEn : s.nameAr }))} />
        </div>
        <Hint>{t('security.buttonsHint')}</Hint>
        <div style={{ overflowX: 'auto', marginTop: 10 }}>
          <table style={tableStyle}>
            <thead>
              <tr>
                <th style={thStyle}>{t('security.button')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.customRule')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.enabled')}</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>{t('security.auditPress')}</th>
              </tr>
            </thead>
            <tbody>
              {(current?.buttons ?? []).map((b) => {
                const row = rules[b.code];
                // Without a rule: what the role's screen permission gives today (from the server).
                const fallbackAllowed = effective?.find((e) => e.buttonCode === b.code && !e.isConfigured)?.isEnabled;
                const update = (patch: Partial<ButtonPermissionRow> | null) =>
                  setRules((prev) => {
                    const next = { ...prev };
                    if (patch === null) delete next[b.code];
                    else next[b.code] = { ...(prev[b.code] ?? { screenCode: screen, buttonCode: b.code, isEnabled: fallbackAllowed ?? true, requiresAuditLog: true }), ...patch };
                    return next;
                  });
                return (
                  <tr key={b.code}>
                    <td style={tdStyle}>
                      <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                        <span>{en ? b.nameEn : b.nameAr}</span>
                        {!b.serverEnforced && <span title={t('security.uiOnlyHint')}><Badge label={t('security.uiOnly')} tone="info" /></span>}
                      </div>
                      <div style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
                        {t('security.fallsBackOn', { action: t(`security.rights.can${b.fallbackAction}`) })}
                      </div>
                    </td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={!!row} disabled={!editable} onChange={(v) => update(v ? {} : null)} /></td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={row ? row.isEnabled : !!fallbackAllowed} disabled={!editable || !row} onChange={(v) => update({ isEnabled: v })} /></td>
                    <td style={{ ...tdStyle, textAlign: 'center' }}><Check checked={row ? row.requiresAuditLog : true} disabled={!editable || !row} onChange={(v) => update({ requiresAuditLog: v })} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </CardBody>
    </Card>
  );
}

function GroupRows({ group, items, screens, editable, onRight, onRow, onColumn }: {
  group: string;
  items: { code: string; name: string }[];
  screens: Record<string, ScreenPermissionRow>;
  editable: boolean;
  onRight: (code: string, right: Right, value: boolean) => void;
  onRow: (code: string, value: boolean) => void;
  onColumn: (codes: string[], right: Right, value: boolean) => void;
}) {
  const codes = items.map((i) => i.code);
  const columnOn = (r: Right) => codes.length > 0 && codes.every((c) => screens[c]?.[r]);
  return (
    <>
      <tr>
        <td style={{ ...tdStyle, fontWeight: 700, background: 'var(--color-surface-2, #f7f8fa)' }}>{group}</td>
        {RIGHTS.map((r) => (
          <td key={r} style={{ ...tdStyle, textAlign: 'center', background: 'var(--color-surface-2, #f7f8fa)' }}>
            <Check checked={columnOn(r)} disabled={!editable} onChange={(v) => onColumn(codes, r, v)} />
          </td>
        ))}
        <td style={{ ...tdStyle, background: 'var(--color-surface-2, #f7f8fa)' }} />
      </tr>
      {items.map((item) => {
        const row = screens[item.code];
        return (
          <tr key={item.code}>
            <td style={{ ...tdStyle, paddingInlineStart: 24 }}>{item.name}</td>
            {RIGHTS.map((r) => (
              <td key={r} style={{ ...tdStyle, textAlign: 'center' }}>
                <Check checked={!!row?.[r]} disabled={!editable} onChange={(v) => onRight(item.code, r, v)} />
              </td>
            ))}
            <td style={{ ...tdStyle, textAlign: 'center' }}>
              <Check checked={!!row && RIGHTS.every((r) => row[r])} disabled={!editable} onChange={(v) => onRow(item.code, v)} />
            </td>
          </tr>
        );
      })}
    </>
  );
}
