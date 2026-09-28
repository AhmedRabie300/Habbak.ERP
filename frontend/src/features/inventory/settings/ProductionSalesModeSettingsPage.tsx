import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePermission } from '../../../ui-kit/usePermission';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useBranchesList } from '../../organization/branches/api';
import { useItemsList } from '../items/api';
import {
  useCreateProductionSalesModeSetting, useDeleteProductionSalesModeSetting,
  useProductionSalesModeSettingsList, useUpdateProductionSalesModeSetting
} from './api';

const SCOPE_TYPES = ['Company', 'Branch', 'POS', 'Item'] as const;
const MODES = ['RealTime', 'Stocked'] as const;

// Rule 17: most specific to least specific.
const SCOPE_PRIORITY: Record<string, number> = { Item: 0, POS: 1, Branch: 2, Company: 3 };

/** /settings/production-sales-mode — screen #21's priority matrix (rule 17: Item &gt; POS &gt; Branch
 * &gt; Company, first matching row wins at resolution time). POS scope has no entity yet
 * (05-Module-POS-Shifts.md isn't built) — its ScopeId is a free-form number until that module exists. */
export function ProductionSalesModeSettingsPage() {
  const { t } = useTranslation();
  const canDelete = usePermission('delete');
  const showToast = useToastStore((s) => s.show);
  const { data: settings, isLoading } = useProductionSalesModeSettingsList();
  const { data: branches } = useBranchesList();
  const { data: items } = useItemsList();

  const createMutation = useCreateProductionSalesModeSetting();
  const updateMutation = useUpdateProductionSalesModeSetting();
  const deleteMutation = useDeleteProductionSalesModeSetting();

  const [scopeType, setScopeType] = useState<(typeof SCOPE_TYPES)[number]>('Company');
  const [scopeId, setScopeId] = useState<number | ''>('');
  const [mode, setMode] = useState<(typeof MODES)[number]>('Stocked');

  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

  const scopeLabel = (row: { scopeType: string; scopeId?: number }) => {
    if (row.scopeType === 'Company') return t('inventorySettings.scopeCompany');
    if (row.scopeType === 'Branch') return branchOptions.find((o) => o.value === row.scopeId)?.label ?? row.scopeId;
    if (row.scopeType === 'Item') return itemOptions.find((o) => o.value === row.scopeId)?.label ?? row.scopeId;
    return `${t('inventorySettings.scopePOS')} #${row.scopeId}`;
  };

  const sortedSettings = [...(settings ?? [])].sort((a, b) => SCOPE_PRIORITY[a.scopeType] - SCOPE_PRIORITY[b.scopeType]);

  const handleAdd = async () => {
    try {
      await createMutation.mutateAsync({
        scopeType,
        scopeId: scopeType === 'Company' ? undefined : (scopeId === '' ? undefined : Number(scopeId)),
        mode
      });
      showToast(t('inventorySettings.addSuccess'), 'success');
      setScopeId('');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleUpdateMode = async (id: number, newMode: string) => {
    try {
      await updateMutation.mutateAsync({ id, mode: newMode });
      showToast(t('inventorySettings.updateSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteMutation.mutateAsync(id);
      showToast(t('inventorySettings.deleteSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('inventorySettings.productionSalesModeTitle')}</h2>

      <Card>
        <CardBody>
          <h3 style={{ marginTop: 0 }}>{t('inventorySettings.addSetting')}</h3>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FieldWrapper label={t('inventorySettings.scopeType')}>
              <SearchableSelect
                style={{ minWidth: 140 }}
                value={scopeType}
                onChange={(v) => { setScopeType(v as (typeof SCOPE_TYPES)[number]); setScopeId(''); }}
                options={SCOPE_TYPES.map((st) => ({ value: st, label: t(`inventorySettings.scope${st}`) }))}
              />
            </FieldWrapper>
            {scopeType === 'Branch' && (
              <FieldWrapper label={t('inventorySettings.scopeId')}>
                <SearchableSelect style={{ minWidth: 200 }} value={scopeId} onChange={(v) => setScopeId(v === '' ? '' : Number(v))} options={branchOptions} />
              </FieldWrapper>
            )}
            {scopeType === 'Item' && (
              <FieldWrapper label={t('inventorySettings.scopeId')}>
                <SearchableSelect style={{ minWidth: 200 }} value={scopeId} onChange={(v) => setScopeId(v === '' ? '' : Number(v))} options={itemOptions} />
              </FieldWrapper>
            )}
            {scopeType === 'POS' && (
              <FieldWrapper label={t('inventorySettings.scopeId')}>
                <input
                  type="number"
                  value={scopeId}
                  onChange={(e) => setScopeId(e.target.value === '' ? '' : Number(e.target.value))}
                  style={{ width: 120, padding: '9px 12px', borderRadius: 'var(--radius-chip)', border: '1px solid transparent', background: 'var(--color-surface-2)' }}
                />
              </FieldWrapper>
            )}
            <FieldWrapper label={t('inventorySettings.mode')}>
              <SearchableSelect
                style={{ minWidth: 140 }}
                value={mode}
                onChange={(v) => setMode(v as (typeof MODES)[number])}
                options={MODES.map((m) => ({ value: m, label: t(`inventorySettings.mode${m}`) }))}
              />
            </FieldWrapper>
            <Button variant="primary" onClick={handleAdd}>{t('inventorySettings.addSetting')}</Button>
          </div>
        </CardBody>
      </Card>

      <Card>
        <CardBody>
          {isLoading ? (
            <div>{t('common.loading')}</div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('inventorySettings.scopeType')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('inventorySettings.scopeId')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('inventorySettings.mode')}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {sortedSettings.map((row) => (
                    <tr key={row.id}>
                      <td style={{ padding: 8 }}>{t(`inventorySettings.scope${row.scopeType}`)}</td>
                      <td style={{ padding: 8 }}>{scopeLabel(row)}</td>
                      <td style={{ padding: 8 }}>
                        <SearchableSelect
                          style={{ width: 140 }}
                          value={row.mode}
                          onChange={(v) => handleUpdateMode(row.id, v)}
                          options={MODES.map((m) => ({ value: m, label: t(`inventorySettings.mode${m}`) }))}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        {canDelete && <Button type="button" variant="ghost" onClick={() => handleDelete(row.id)}>{t('common.remove')}</Button>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
