import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useAccountsList } from '../accounts/api';
import { useAccountMappings, useUpdateAccountMappings } from './api';

const TYPE_ORDER = ['Asset', 'Liability', 'Revenue', 'Expense'] as const;

const typeLabelKey = (type: string) => `accounts.${type.charAt(0).toLowerCase() + type.slice(1)}Type`;

/**
 * /accounting/settings/account-mappings — the company-level accounts the posting engine resolves by
 * role (Docs/Posting-Engine-Implementation-Plan.md, stage 0). Filled in by the finance manager.
 *
 * Each role's dropdown only offers postable accounts of the type that role needs; the server
 * enforces the same rule, this just keeps the wrong choice from being offered in the first place.
 * Only edited rows are sent, so saving half the list never clears the other half.
 */
export function AccountMappingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: mappings, isLoading } = useAccountMappings();
  const { data: accounts } = useAccountsList(true);
  const updateMutation = useUpdateAccountMappings();

  const [values, setValues] = useState<Record<string, number | null>>({});
  const [dirty, setDirty] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (mappings) {
      setValues(Object.fromEntries(mappings.map((m) => [m.role, m.accountId])));
      setDirty(new Set());
    }
  }, [mappings]);

  const optionsByType = useMemo(() => {
    const byType: Record<string, { value: number; label: string }[]> = {};
    for (const a of accounts ?? []) {
      (byType[a.accountType] ??= []).push({ value: a.id, label: `${a.code} — ${a.nameAr}` });
    }
    return byType;
  }, [accounts]);

  const setRole = (role: string, accountId: number | null) => {
    setValues((prev) => ({ ...prev, [role]: accountId }));
    setDirty((prev) => new Set(prev).add(role));
  };

  const handleSave = async () => {
    if (dirty.size === 0) return;
    try {
      await updateMutation.mutateAsync([...dirty].map((role) => ({ role, accountId: values[role] ?? null })));
      showToast(t('accountMappings.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading || !mappings) {
    return <div>{t('common.loading')}</div>;
  }

  const mappedCount = mappings.filter((m) => values[m.role] != null).length;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', flexWrap: 'wrap', gap: 8 }}>
        <h2 style={{ margin: 0 }}>{t('accountMappings.title')}</h2>
        <span style={{ fontSize: 13, color: 'var(--color-text-muted)' }}>
          {t('accountMappings.progress', { mapped: mappedCount, total: mappings.length })}
        </span>
      </div>

      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave, disabled: dirty.size === 0 }} />

      <div style={{ padding: '10px 14px', background: 'var(--color-surface-2)', border: '1px solid var(--color-border)', borderRadius: 6, fontSize: 13, lineHeight: 1.8 }}>
        {t('accountMappings.note')}
      </div>

      {TYPE_ORDER.map((type) => {
        const rows = mappings.filter((m) => m.expectedAccountType === type);
        if (rows.length === 0) return null;

        return (
          <Card key={type}>
            <CardBody>
              <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>{t(typeLabelKey(type))}</div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                {rows.map((m) => {
                  const value = values[m.role];
                  const isMapped = value != null;
                  return (
                    <div
                      key={m.role}
                      style={{
                        display: 'grid', gridTemplateColumns: 'minmax(220px, 1fr) minmax(260px, 1.4fr) auto',
                        gap: 12, alignItems: 'center', padding: '8px 0', borderTop: '1px solid var(--color-border)'
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 600, fontSize: 13.5, display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span
                            aria-hidden
                            style={{
                              width: 8, height: 8, borderRadius: '50%',
                              background: isMapped ? 'var(--color-success, #2E7D5B)' : 'var(--color-border)'
                            }}
                          />
                          {t(`accountMappings.role.${m.role}`)}
                        </div>
                        <div style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 2 }}>
                          {t(`accountMappings.hint.${m.role}`)}
                        </div>
                      </div>

                      <SearchableSelect
                        style={{ width: '100%' }}
                        value={value ?? ''}
                        onChange={(v) => setRole(m.role, v === '' ? null : Number(v))}
                        options={optionsByType[type] ?? []}
                        placeholder={t('accountMappings.notMapped')}
                      />

                      <button
                        type="button"
                        onClick={() => setRole(m.role, null)}
                        disabled={!isMapped}
                        // visibility, not a transparent colour: keeps the column width stable
                        // without leaving an invisible button in the tab order and screen readers.
                        style={{
                          background: 'transparent', border: 'none', fontSize: 12.5,
                          color: 'var(--color-text-muted)', cursor: 'pointer',
                          visibility: isMapped ? 'visible' : 'hidden'
                        }}
                      >
                        {t('accountMappings.clear')}
                      </button>
                    </div>
                  );
                })}
              </div>
            </CardBody>
          </Card>
        );
      })}
    </div>
  );
}
