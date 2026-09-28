import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useBranchesList } from '../../organization/branches/api';
import { useBranchPOSSettings, useUpdateBranchPOSSettings } from './api';
import type { BranchPOSSettings } from './types';

const OPERATION_MODES = ['CloudOnly', 'OfflineOnly', 'HybridAutoSync'] as const;
const POSTING_MODES = ['PerTransaction', 'PerShift', 'PerDay'] as const;

const DEFAULTS: Omit<BranchPOSSettings, 'branchId'> = {
  operationMode: 'HybridAutoSync',
  tipsEnabled: false,
  serviceChargeEnabled: false,
  serviceChargeRate: 0,
  vatEnabled: false,
  vatRate: 14,
  allowSplitPayment: false,
  loyaltyRedemptionEnabledAtPOS: false,
  etaReceiptEnabled: false,
  cashRoundingIncrement: 0,
  maxAllowedShiftCashDifference: 100,
  postingMode: 'PerShift',
  shiftVarianceEmployeeLiabilityThreshold: 10
};

const CHECKBOX_FIELDS: { key: keyof Omit<BranchPOSSettings, 'branchId' | 'operationMode' | 'serviceChargeRate' | 'vatRate' | 'cashRoundingIncrement' | 'maxAllowedShiftCashDifference' | 'postingMode' | 'shiftVarianceEmployeeLiabilityThreshold'>; labelKey: string }[] = [
  { key: 'tipsEnabled', labelKey: 'posSettings.tipsEnabled' },
  { key: 'serviceChargeEnabled', labelKey: 'posSettings.serviceChargeEnabled' },
  { key: 'vatEnabled', labelKey: 'posSettings.vatEnabled' },
  { key: 'allowSplitPayment', labelKey: 'posSettings.allowSplitPayment' },
  { key: 'loyaltyRedemptionEnabledAtPOS', labelKey: 'posSettings.loyaltyRedemptionEnabledAtPOS' },
  { key: 'etaReceiptEnabled', labelKey: 'posSettings.etaReceiptEnabled' }
];

/** /pos/branch-settings — إعدادات نقطة البيع لكل فرع، قرارات جلسة الاستشارة قبل التنفيذ
 * (05-Module-POS-Shifts.md). صف واحد لكل فرع؛ الفرع يُختار أولًا من قائمة منسدلة. */
export function BranchPOSSettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: branches } = useBranchesList();
  const [branchId, setBranchId] = useState<number | ''>('');

  const { data: settings, isLoading } = useBranchPOSSettings(branchId === '' ? undefined : branchId);
  const updateMutation = useUpdateBranchPOSSettings(branchId === '' ? 0 : branchId);

  const [form, setForm] = useState<Omit<BranchPOSSettings, 'branchId'>>(DEFAULTS);

  useEffect(() => {
    if (branches && branches.length > 0 && branchId === '') {
      setBranchId(branches[0].id);
    }
  }, [branches, branchId]);

  useEffect(() => {
    if (settings) setForm(settings);
  }, [settings]);

  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }));

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync(form);
      showToast(t('posSettings.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('posSettings.title')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave, disabled: branchId === '' }} />

      <Card>
        <CardBody>
          <FieldWrapper label={t('posSettings.branch')}>
            <SearchableSelect style={{ minWidth: 220 }} value={branchId} onChange={(v) => setBranchId(v === '' ? '' : Number(v))} options={branchOptions} />
          </FieldWrapper>

          {branchId !== '' && isLoading && <div>{t('common.loading')}</div>}

          {branchId !== '' && !isLoading && (
            <>
              <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginTop: 16 }}>
                <FieldWrapper label={t('posSettings.operationMode')}>
                  <SearchableSelect
                    style={{ minWidth: 180 }}
                    value={form.operationMode}
                    onChange={(v) => setForm((prev) => ({ ...prev, operationMode: String(v) }))}
                    options={OPERATION_MODES.map((m) => ({ value: m, label: t(`posSettings.mode${m}`) }))}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('posSettings.serviceChargeRate')}>
                  <Input
                    type="number" step="0.01" style={{ width: 120 }}
                    value={form.serviceChargeRate}
                    onChange={(e) => setForm((prev) => ({ ...prev, serviceChargeRate: Number(e.target.value) }))}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('posSettings.vatRate')}>
                  <Input
                    type="number" step="0.01" style={{ width: 120 }}
                    value={form.vatRate}
                    onChange={(e) => setForm((prev) => ({ ...prev, vatRate: Number(e.target.value) }))}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('posSettings.cashRoundingIncrement')}>
                  <Input
                    type="number" step="0.01" style={{ width: 120 }}
                    value={form.cashRoundingIncrement}
                    onChange={(e) => setForm((prev) => ({ ...prev, cashRoundingIncrement: Number(e.target.value) }))}
                  />
                </FieldWrapper>
                <FieldWrapper label={t('posSettings.maxAllowedShiftCashDifference')}>
                  <Input
                    type="number" step="0.01" style={{ width: 140 }}
                    value={form.maxAllowedShiftCashDifference}
                    onChange={(e) => setForm((prev) => ({ ...prev, maxAllowedShiftCashDifference: Number(e.target.value) }))}
                  />
                </FieldWrapper>
              </div>

              <div style={{ marginTop: 20, paddingTop: 16, borderTop: '1px solid var(--color-border)' }}>
                <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 10 }}>{t('posSettings.postingSection')}</div>
                <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-start' }}>
                  <FieldWrapper label={t('posSettings.postingMode')}>
                    <SearchableSelect
                      value={form.postingMode}
                      onChange={(v) => setForm((prev) => ({ ...prev, postingMode: v as BranchPOSSettings['postingMode'] }))}
                      options={POSTING_MODES.map((m) => ({ value: m, label: t(`posSettings.postingModes.${m}`) }))}
                      style={{ minWidth: 260 }}
                    />
                  </FieldWrapper>
                  <FieldWrapper label={t('posSettings.shiftVarianceEmployeeLiabilityThreshold')}>
                    <Input
                      type="number" step="0.01" min={0} style={{ width: 140 }}
                      value={form.shiftVarianceEmployeeLiabilityThreshold}
                      onChange={(e) => setForm((prev) => ({ ...prev, shiftVarianceEmployeeLiabilityThreshold: Number(e.target.value) }))}
                    />
                  </FieldWrapper>
                </div>
                <p style={{ margin: '8px 0 0', fontSize: 12.5, color: 'var(--color-text-muted)', lineHeight: 1.8, maxWidth: 720 }}>
                  {t(`posSettings.postingModeHint.${form.postingMode}`)} {t('posSettings.postingModeSwitchNote')}
                </p>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 10, marginTop: 20 }}>
                {CHECKBOX_FIELDS.map(({ key, labelKey }) => (
                  <label key={key} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13.5 }}>
                    <input
                      type="checkbox"
                      checked={form[key] as boolean}
                      onChange={(e) => setForm((prev) => ({ ...prev, [key]: e.target.checked }))}
                    />
                    {t(labelKey)}
                  </label>
                ))}
              </div>
            </>
          )}
        </CardBody>
      </Card>
    </div>
  );
}
