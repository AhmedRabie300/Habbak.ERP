import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePurchaseCyclePresets, usePurchaseCycleSettings, useUpdatePurchaseCycleSettings } from './api';
import type { PurchaseCycleSettings } from './types';

const CYCLE_TYPES = ['Full', 'Direct', 'OrderBased', 'RequestBased', 'Simplified'] as const;
const PAYMENT_TERMS = ['Cash', 'Net15', 'Net30', 'Net60'] as const;

const DEFAULTS: PurchaseCycleSettings = {
  cycleType: 'Full',
  requiresPurchaseRequest: false,
  requiresQuotation: false,
  requiresPurchaseOrder: true,
  requiresGoodsReceipt: true,
  allowInvoiceWithoutOrder: false,
  allowReceiptWithoutInvoice: true,
  autoCreateReceiptOnInvoicePost: false,
  autoCreateInvoiceOnReceipt: false,
  requiresApprovalForPurchaseOrder: false,
  requiresApprovalForInvoice: false,
  defaultPaymentTerms: 'Net30',
  capitalizeAdditionalCosts: true,
  allowManualInvoiceLines: true
};

// Remarks4 item 6: every flag on this screen is now read by a command handler — the audit and what
// each one does is in Docs/Modules/Purchasing-Settings-Audit.md. The four document flags belong to
// the cycle type and are filled from its preset; the rest are free choices within that cycle.
const DOCUMENT_FIELDS: (keyof PurchaseCycleSettings)[] = [
  'requiresPurchaseRequest',
  'requiresQuotation',
  'requiresPurchaseOrder',
  'requiresGoodsReceipt',
  'allowInvoiceWithoutOrder',
  'allowReceiptWithoutInvoice'
];

const CHECKBOX_FIELDS: { key: keyof PurchaseCycleSettings; labelKey: string }[] = [
  { key: 'requiresPurchaseRequest', labelKey: 'purchaseCycleSettings.requiresPurchaseRequest' },
  { key: 'requiresQuotation', labelKey: 'purchaseCycleSettings.requiresQuotation' },
  { key: 'requiresPurchaseOrder', labelKey: 'purchaseCycleSettings.requiresPurchaseOrder' },
  { key: 'requiresGoodsReceipt', labelKey: 'purchaseCycleSettings.requiresGoodsReceipt' },
  { key: 'allowInvoiceWithoutOrder', labelKey: 'purchaseCycleSettings.allowInvoiceWithoutOrder' },
  { key: 'allowReceiptWithoutInvoice', labelKey: 'purchaseCycleSettings.allowReceiptWithoutInvoice' },
  { key: 'autoCreateReceiptOnInvoicePost', labelKey: 'purchaseCycleSettings.autoCreateReceiptOnInvoicePost' },
  { key: 'autoCreateInvoiceOnReceipt', labelKey: 'purchaseCycleSettings.autoCreateInvoiceOnReceipt' },
  { key: 'requiresApprovalForPurchaseOrder', labelKey: 'purchaseCycleSettings.requiresApprovalForPurchaseOrder' },
  { key: 'requiresApprovalForInvoice', labelKey: 'purchaseCycleSettings.requiresApprovalForInvoice' },
  { key: 'capitalizeAdditionalCosts', labelKey: 'purchaseCycleSettings.capitalizeAdditionalCosts' },
  { key: 'allowManualInvoiceLines', labelKey: 'purchaseCycleSettings.allowManualInvoiceLines' }
];

/** /purchasing/settings/purchase-cycle — screen #11 (03-Module-Purchasing.md, section 8). One row
 * per company; each checkbox mirrors a PurchaseCycleSettings flag other Purchasing commands will
 * check as they get built (e.g. PurchaseInvoice's create command checking AllowInvoiceWithoutOrder). */
export function PurchaseCycleSettingsPage() {
  const { t } = useTranslation();
  const showToast = useToastStore((s) => s.show);
  const { data: settings, isLoading } = usePurchaseCycleSettings();
  const { data: presets } = usePurchaseCyclePresets();
  const updateMutation = useUpdatePurchaseCycleSettings();

  const [form, setForm] = useState<PurchaseCycleSettings>(DEFAULTS);

  useEffect(() => {
    if (settings) setForm(settings);
  }, [settings]);

  /** The document flags of a named cycle, as the backend defines them. */
  const presetFlags = (cycleType: string): Partial<PurchaseCycleSettings> => {
    const preset = (presets ?? []).find((p) => p.cycleType === cycleType);
    if (!preset) return {};
    return {
      requiresPurchaseRequest: preset.requiresPurchaseRequest,
      requiresQuotation: preset.requiresQuotation,
      requiresPurchaseOrder: preset.requiresPurchaseOrder,
      requiresGoodsReceipt: preset.requiresGoodsReceipt,
      allowInvoiceWithoutOrder: preset.allowInvoiceWithoutOrder,
      allowReceiptWithoutInvoice: preset.allowReceiptWithoutInvoice
    };
  };

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync(form);
      showToast(t('purchaseCycleSettings.saveSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('purchaseCycleSettings.title')}</h2>
      <ActionBar primary={{ key: 'save', label: t('common.saveChanges'), onClick: handleSave }} />

      <div style={{ padding: '10px 14px', background: 'var(--color-info-bg, #eef4ff)', borderRadius: 6, fontSize: 13 }}>
        {t('purchaseCycleSettings.cycleTypeNote')}
      </div>

      <Card>
        <CardBody>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <FieldWrapper label={t('purchaseCycleSettings.cycleType')}>
              <SearchableSelect
                style={{ minWidth: 180 }}
                value={form.cycleType}
                onChange={(v) => setForm((prev) => ({ ...prev, cycleType: v, ...presetFlags(v) }))}
                options={CYCLE_TYPES.map((ct) => ({ value: ct, label: t(`purchaseCycleSettings.cycle${ct}`) }))}
              />
            </FieldWrapper>
            <FieldWrapper label={t('purchaseCycleSettings.defaultPaymentTerms')}>
              <SearchableSelect
                style={{ minWidth: 140 }}
                value={form.defaultPaymentTerms}
                onChange={(v) => setForm((prev) => ({ ...prev, defaultPaymentTerms: v }))}
                options={PAYMENT_TERMS.map((pt) => ({ value: pt, label: t(`suppliers.terms${pt}`) }))}
              />
            </FieldWrapper>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 10, marginTop: 20 }}>
            {CHECKBOX_FIELDS.map(({ key, labelKey }) => {
              const fromCycle = DOCUMENT_FIELDS.includes(key);
              return (
                <label key={key} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13.5 }}>
                  <input
                    type="checkbox"
                    checked={form[key] as boolean}
                    disabled={fromCycle}
                    onChange={(e) => setForm((prev) => ({ ...prev, [key]: e.target.checked }))}
                  />
                  {t(labelKey)}
                  {fromCycle && (
                    <span style={{ fontSize: 11, color: 'var(--color-text-muted)', border: '1px solid var(--color-border)', borderRadius: 4, padding: '1px 5px' }}>
                      {t('purchaseCycleSettings.fromCycleBadge')}
                    </span>
                  )}
                </label>
              );
            })}
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
