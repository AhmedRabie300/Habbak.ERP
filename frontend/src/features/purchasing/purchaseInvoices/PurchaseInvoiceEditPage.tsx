import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { AttachmentPanel } from '../../../ui-kit/AttachmentPanel';
import { PurchaseInvoiceSourceOrderPanel } from '../common/SourceDocumentPanels';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useMandatorySettings } from '../../settings/codingRules/useMandatorySettings';
import { useSuppliersList } from '../suppliers/api';
import { useItemsList } from '../../inventory/items/api';
import { UnitSelect, defaultUnitId } from '../../inventory/items/UnitSelect';
import { useApplyDefaultCurrency, useCurrenciesList } from '../../organization/currencies/api';
import { useAccountsList } from '../../accounting/accounts/api';
import { useOpenPurchaseOrders, usePurchaseOrderLinesForInvoice } from './orderLinkingApi';
import { usePurchaseCycleSettings } from '../settings/api';
import { useGoodsReceiptsList } from '../goodsReceipts/api';
import {
  useCancelPurchaseInvoice, useCreatePurchaseInvoice, usePostPurchaseInvoice, usePurchaseInvoice,
  useRejectPurchaseInvoice, useSubmitPurchaseInvoice, useUpdatePurchaseInvoice
} from './api';
import type { PurchaseInvoiceLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const PAYMENT_TERMS = ['Cash', 'Net15', 'Net30', 'Net60'] as const;
const ALLOCATION_METHODS = ['ByValue', 'ByQuantity', 'ByWeight', 'Manual'] as const;

interface FormValues {
  supplierId: number | '';
  supplierInvoiceNumber: string;
  purchaseOrderId: number | '';
  goodsReceiptId: number | '';
  invoiceDate: string;
  dueDate: string;
  currencyCode: string | '';
  exchangeRate: number;
  paymentTerms: (typeof PAYMENT_TERMS)[number];
  taxAmount: number;
  discountAmount: string;
  discountReason: string;
  additionalCosts: number;
  additionalCostAllocationMethod: (typeof ALLOCATION_METHODS)[number] | '';
  commissionRate: string;
  commissionAmount: string;
  commissionAccountId: number | '';
  notes: string;
  lines: PurchaseInvoiceLineInput[];
}

const emptyLine = (): PurchaseInvoiceLineInput => ({ itemId: 0, quantity: 0, receivedQuantity: 0, unitPrice: 0, unitId: undefined });

/** /purchasing/purchase-invoices/:id — screen #5 (03-Module-Purchasing.md, section 8). Draft →
 * PendingApproval (Submit) → Posted (section 6.4) — editing locks after Draft (rule 15). Linking a
 * PurchaseOrder/GoodsReceipt is set once at creation only, mirroring PurchaseOrder's own
 * purchaseRequestId (new orders only) pattern. AllocatedAdditionalCost/AllocationPercentage columns
 * implement the AdditionalCostAllocationMethod spread (ByValue/ByQuantity/ByWeight/Manual). */
export function PurchaseInvoiceEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const invoiceId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: invoice, isLoading } = usePurchaseInvoice(invoiceId);
  const { data: suppliers } = useSuppliersList();
  const { data: cycleSettings } = usePurchaseCycleSettings();
  const allowManualLines = cycleSettings?.allowManualInvoiceLines ?? true;
  const { data: goodsReceiptsPage } = useGoodsReceiptsList({ page: 1, pageSize: 200 });
  const { data: items } = useItemsList();
  const { data: currencies } = useCurrenciesList();
  const { data: accounts } = useAccountsList();
  const { label } = useFieldLabels('PURCHASING_PURCHASE_INVOICE');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_PURCHASE_INVOICE', 'PurchaseInvoice', invoiceId);

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      supplierId: '', supplierInvoiceNumber: '', purchaseOrderId: '', goodsReceiptId: '',
      invoiceDate: todayLocal(), dueDate: todayLocal(),
      currencyCode: '', exchangeRate: 1, paymentTerms: 'Net30', taxAmount: 0, discountAmount: '', discountReason: '',
      additionalCosts: 0, additionalCostAllocationMethod: '', commissionRate: '', commissionAmount: '',
      commissionAccountId: '', notes: '', lines: [emptyLine()]
    }
  });
  // Remarks3 item 1: a new record starts in the default currency.
  useApplyDefaultCurrency(isNew, watch('currencyCode'), (code) => setValue('currencyCode', code), 'code');
  const { fields, append, remove, replace } = useFieldArray({ control, name: 'lines' });

  // Remarks6: the supplier decides which orders are on offer, and the chosen order fills the lines.
  const watchedSupplierId = watch('supplierId');
  const watchedOrderId = watch('purchaseOrderId');
  const { data: openOrders } = useOpenPurchaseOrders(watchedSupplierId === '' ? undefined : Number(watchedSupplierId));
  const { data: orderLines } = usePurchaseOrderLinesForInvoice(
    watchedOrderId === '' ? undefined : Number(watchedOrderId), invoiceId);

  const remainingOf = (orderLineId: number | undefined) =>
    (orderLines ?? []).find((l) => l.purchaseOrderLineId === orderLineId)?.remainingQuantity ?? 0;

  /** Everything the chosen order still owes, as invoice lines the user can trim before saving. */
  const loadOrderLines = () => {
    replace(
      (orderLines ?? []).map((l) => ({
        itemId: l.itemId,
        purchaseOrderLineId: l.purchaseOrderLineId,
        quantity: l.remainingQuantity,
        receivedQuantity: Math.min(l.receivedQuantity, l.remainingQuantity),
        unitPrice: l.unitPrice,
        discountAmount: l.discountAmount,
        unitId: l.unitId,
        allocationPercentage: undefined,
        weight: undefined
      }))
    );
  };

  /** Picking a different order throws away what the previous one filled in — the lines belong to it. */
  const chooseOrder = (value: number | '') => {
    const hasLines = (watch('lines') ?? []).some((l) => Number(l.itemId) > 0);
    if (hasLines && !window.confirm(t('purchaseInvoices.replaceLinesConfirm'))) {
      return false;
    }

    setValue('purchaseOrderId', value);
    if (value === '') {
      replace([emptyLine()]);
    }

    return true;
  };

  /** Auto-loads the moment the chosen order's lines arrive — the "تحميل سطور الأمر" button stays
   * only as a manual reload for after the user has edited lines by hand. Guarded to isNew: for an
   * existing invoice orderLines is fetched too (with excludeInvoiceId, for the remaining-column and
   * cap while editing), but reset() already loaded the invoice's real saved lines — auto-replacing
   * them here with a fresh "what's left" recomputation would silently overwrite real data. */
  const lastAutoLoadedOrderId = useRef<number | ''>('');
  useEffect(() => {
    if (!isNew) return;
    if (watchedOrderId === '') {
      lastAutoLoadedOrderId.current = '';
      return;
    }
    if (watchedOrderId === lastAutoLoadedOrderId.current || orderLines === undefined) {
      return;
    }

    lastAutoLoadedOrderId.current = watchedOrderId;
    loadOrderLines();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isNew, watchedOrderId, orderLines]);
  // Each line's unit is one of its item's units (Fixes-Batch-2026-09-19).
  const unitLines = watch('lines');
  const unitItems = new Map((items ?? []).map((i) => [i.id, i]));

  useEffect(() => {
    if (invoice) {
      reset({
        supplierId: invoice.supplierId,
        supplierInvoiceNumber: invoice.supplierInvoiceNumber ?? '',
        purchaseOrderId: invoice.purchaseOrderId ?? '',
        goodsReceiptId: invoice.goodsReceiptId ?? '',
        invoiceDate: invoice.invoiceDate,
        dueDate: invoice.dueDate,
        currencyCode: invoice.currencyCode,
        exchangeRate: invoice.exchangeRate,
        paymentTerms: invoice.paymentTerms as (typeof PAYMENT_TERMS)[number],
        taxAmount: invoice.taxAmount,
        discountAmount: invoice.discountAmount?.toString() ?? '',
        discountReason: invoice.discountReason ?? '',
        additionalCosts: invoice.additionalCosts,
        additionalCostAllocationMethod: (invoice.additionalCostAllocationMethod as (typeof ALLOCATION_METHODS)[number] | undefined) ?? '',
        commissionRate: invoice.commissionRate?.toString() ?? '',
        commissionAmount: invoice.commissionAmount?.toString() ?? '',
        commissionAccountId: invoice.commissionAccountId ?? '',
        notes: invoice.notes ?? '',
        lines: invoice.lines.map((l) => ({
          purchaseOrderLineId: l.purchaseOrderLineId,
          itemId: l.itemId, quantity: l.quantity, receivedQuantity: l.receivedQuantity, unitPrice: l.unitPrice,
          discountAmount: l.discountAmount, unitId: l.unitId, allocationPercentage: l.allocationPercentage, weight: l.weight
        }))
      });
    }
  }, [invoice, reset]);

  const createMutation = useCreatePurchaseInvoice();
  const updateMutation = useUpdatePurchaseInvoice(invoiceId ?? 0);
  const submitMutation = useSubmitPurchaseInvoice(invoiceId ?? 0);
  const postMutation = usePostPurchaseInvoice(invoiceId ?? 0);
  const rejectMutation = useRejectPurchaseInvoice(invoiceId ?? 0);
  const cancelMutation = useCancelPurchaseInvoice(invoiceId ?? 0);

  const isEditable = isNew || invoice?.status === 'Draft';
  const canSubmit = !isNew && invoice?.status === 'Draft';
  const canPost = !isNew && (invoice?.status === 'Draft' || invoice?.status === 'PendingApproval');
  const canReject = !isNew && (invoice?.status === 'Draft' || invoice?.status === 'PendingApproval');
  // Posted but unpaid can be cancelled too; the server posts the reversing entry and refuses if
  // goods were received or a return exists.
  const canCancel = canReject || (!isNew && invoice?.status === 'Posted');

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const purchaseOrderOptions = (openOrders ?? []).map((o) => ({
    value: o.id,
    label: `${o.orderNumber} — ${o.orderDate} — ${o.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${o.currencyCode} — ${t('purchaseInvoices.remainingLines', { count: o.remainingLineCount })}`
  }));
  const goodsReceiptOptions = (goodsReceiptsPage?.items ?? []).map((r) => ({ value: r.id, label: r.receiptNumber }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const currencyOptions = (currencies ?? []).map((c) => ({ value: c.code, label: `${c.code} — ${c.nameAr}` }));
  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    try {
      await action();
      showToast(successMessage, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const onSave = handleSubmit(async (values) => {
    if (!checkDescriptionMandatory(values.notes)) return;
    const lines = values.lines.map((l) => ({
      itemId: Number(l.itemId), purchaseOrderLineId: l.purchaseOrderLineId ? Number(l.purchaseOrderLineId) : undefined,
      quantity: Number(l.quantity), receivedQuantity: Number(l.receivedQuantity || 0),
      unitPrice: Number(l.unitPrice), discountAmount: l.discountAmount ? Number(l.discountAmount) : undefined,
      unitId: l.unitId ? Number(l.unitId) : undefined, allocationPercentage: l.allocationPercentage ? Number(l.allocationPercentage) : undefined,
      weight: l.weight ? Number(l.weight) : undefined
    }));
    const common = {
      invoiceDate: values.invoiceDate,
      dueDate: values.dueDate,
      supplierId: Number(values.supplierId),
      supplierInvoiceNumber: values.supplierInvoiceNumber || undefined,
      purchaseOrderId: values.purchaseOrderId === '' ? undefined : Number(values.purchaseOrderId),
      goodsReceiptId: values.goodsReceiptId === '' ? undefined : Number(values.goodsReceiptId),
      currencyCode: String(values.currencyCode),
      exchangeRate: Number(values.exchangeRate),
      paymentTerms: values.paymentTerms,
      taxAmount: Number(values.taxAmount),
      discountAmount: values.discountAmount ? Number(values.discountAmount) : undefined,
      discountReason: values.discountReason || undefined,
      additionalCosts: Number(values.additionalCosts),
      additionalCostAllocationMethod: values.additionalCostAllocationMethod || undefined,
      commissionRate: values.commissionRate ? Number(values.commissionRate) : undefined,
      commissionAmount: values.commissionAmount ? Number(values.commissionAmount) : undefined,
      commissionAccountId: values.commissionAccountId === '' ? undefined : Number(values.commissionAccountId),
      notes: values.notes || undefined,
      lines
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('purchaseInvoices.createSuccess'), 'success');
        navigate(`/purchasing/purchase-invoices/${newId}`);
      } else if (invoice) {
        await updateMutation.mutateAsync({ ...common, rowVersion: invoice.rowVersion });
        showToast(t('purchaseInvoices.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('purchaseInvoices.addPurchaseInvoice') : `${t('purchaseInvoices.title')} — ${invoice?.invoiceNumber ?? ''}`}</h2>
        {invoice && <StatusBadge status={invoice.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/purchase-invoices') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(canSubmit ? [{ key: 'submit', buttonCode: 'Submit', label: t('purchaseInvoices.submit'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => submitMutation.mutateAsync(), t('purchaseInvoices.submitSuccess')); } }] : []),
          ...(canPost ? [{ key: 'post', buttonCode: 'Post', label: t('purchaseInvoices.post'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => postMutation.mutateAsync(), t('purchaseInvoices.postSuccess')); } }] : [])
        ]}
        destructive={[
          ...(canReject ? [{ key: 'reject', buttonCode: 'Reject', label: t('purchaseInvoices.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('purchaseInvoices.rejectSuccess')), confirmMessage: t('purchaseInvoices.rejectConfirm') }] : []),
          ...(canCancel
            ? [{
                key: 'cancel', buttonCode: 'Cancel',
                label: t('purchaseInvoices.cancel'),
                onClick: () => runAction(() => cancelMutation.mutateAsync(), t('purchaseInvoices.cancelSuccess')),
                confirmMessage: t(invoice?.journalEntryId ? 'purchaseInvoices.cancelPostedConfirm' : 'purchaseInvoices.cancelConfirm')
              }]
            : [])
        ]}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('invoiceNumber', t('purchaseInvoices.invoiceNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (invoice?.invoiceNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('supplier', t('purchaseInvoices.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('supplierInvoiceNumber', t('purchaseInvoices.supplierInvoiceNumber'))}>
                <Input disabled={!isEditable} style={{ width: 160 }} {...register('supplierInvoiceNumber')} />
              </FieldWrapper>
              {isNew ? (
                <FieldWrapper label={label('purchaseOrder', t('purchaseInvoices.purchaseOrder'))}>
                  <Controller
                    control={control}
                    name="purchaseOrderId"
                    render={({ field }) => (
                      <SearchableSelect
                        style={{ minWidth: 320 }}
                        value={field.value}
                        onChange={(v) => chooseOrder(v === '' ? '' : Number(v))}
                        options={[{ value: '', label: t('purchaseInvoices.noPurchaseOrder') }, ...purchaseOrderOptions]}
                      />
                    )}
                  />
                </FieldWrapper>
              ) : (
                invoice?.purchaseOrderNumber && (
                  <FieldWrapper label={label('purchaseOrder', t('purchaseInvoices.purchaseOrder'))}>
                    <Input value={invoice.purchaseOrderNumber} disabled />
                  </FieldWrapper>
                )
              )}
              {isNew ? (
                <FieldWrapper label={label('goodsReceipt', t('purchaseInvoices.goodsReceipt'))}>
                  <Controller
                    control={control}
                    name="goodsReceiptId"
                    render={({ field }) => (
                      <SearchableSelect style={{ minWidth: 180 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={goodsReceiptOptions} />
                    )}
                  />
                </FieldWrapper>
              ) : (
                invoice?.goodsReceiptNumber && (
                  <FieldWrapper label={label('goodsReceipt', t('purchaseInvoices.goodsReceipt'))}>
                    <Input value={invoice.goodsReceiptNumber} disabled />
                  </FieldWrapper>
                )
              )}
              <FieldWrapper label={label('invoiceDate', t('purchaseInvoices.invoiceDate'))}>
                <Input type="date" disabled={!isEditable} {...register('invoiceDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('dueDate', t('purchaseInvoices.dueDate'))}>
                <Input type="date" disabled={!isEditable} {...register('dueDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('currency', t('purchaseInvoices.currency'))}>
                <Controller
                  control={control}
                  name="currencyCode"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 140 }} value={field.value} onChange={(v) => field.onChange(v)} options={currencyOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('exchangeRate', t('purchaseInvoices.exchangeRate'))}>
                <Input type="number" step="0.000001" disabled={!isEditable} style={{ width: 120 }} {...register('exchangeRate', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('paymentTerms', t('purchaseInvoices.paymentTerms'))}>
                <Controller
                  control={control}
                  name="paymentTerms"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={PAYMENT_TERMS.map((pt) => ({ value: pt, label: t(`suppliers.terms${pt}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('taxAmount', t('purchaseInvoices.taxAmount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('taxAmount', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('discountAmount', t('purchaseInvoices.discountAmount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('discountAmount')} />
              </FieldWrapper>
              <FieldWrapper label={label('discountReason', t('purchaseInvoices.discountReason'))}>
                <Input disabled={!isEditable} style={{ minWidth: 180 }} {...register('discountReason')} />
              </FieldWrapper>
              <FieldWrapper label={label('additionalCosts', t('purchaseInvoices.additionalCosts'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('additionalCosts', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('additionalCostAllocationMethod', t('purchaseInvoices.additionalCostAllocationMethod'))}>
                <Controller
                  control={control}
                  name="additionalCostAllocationMethod"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={[{ value: '', label: '—' }, ...ALLOCATION_METHODS.map((m) => ({ value: m, label: t(`purchaseInvoices.allocation${m}`) }))]}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('commissionRate', t('purchaseInvoices.commissionRate'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register('commissionRate')} />
              </FieldWrapper>
              <FieldWrapper label={label('commissionAmount', t('purchaseInvoices.commissionAmount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('commissionAmount')} />
              </FieldWrapper>
              <FieldWrapper label={label('commissionAccount', t('purchaseInvoices.commissionAccount'))}>
                <Controller
                  control={control}
                  name="commissionAccountId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={[{ value: '', label: '—' }, ...accountOptions]} />
                  )}
                />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('purchaseInvoices.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('purchaseInvoices.quantity'))}</th>
                    {watchedOrderId !== '' && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{t('purchaseInvoices.remainingOnOrder')}</th>}
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('purchaseInvoices.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineDiscount', t('purchaseInvoices.lineDiscount'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('purchaseInvoices.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('weight', t('purchaseInvoices.weight'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('allocationPercentage', t('purchaseInvoices.allocationPercentage'))}</th>
                    {!isNew && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('receivedQuantity', t('purchaseInvoices.receivedQuantity'))}</th>}
                    {!isNew && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('allocatedAdditionalCost', t('purchaseInvoices.allocatedAdditionalCost'))}</th>}
                    {isEditable && <th />}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.itemId` as const}
                          render={({ field }) => (
                            <SearchableSelect
                              disabled={!isEditable}
                              style={{ width: 220 }}
                              value={field.value}
                              onChange={(v) => {
                                field.onChange(Number(v));
                                setValue(`lines.${index}.unitId`, defaultUnitId(unitItems.get(Number(v))));
                              }}
                              options={[{ value: 0, label: t('purchaseInvoices.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input
                          type="number"
                          step="0.0001"
                          disabled={!isEditable}
                          max={unitLines[index]?.purchaseOrderLineId ? remainingOf(unitLines[index]?.purchaseOrderLineId) : undefined}
                          style={{ width: 100 }}
                          {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })}
                        />
                      </td>
                      {watchedOrderId !== '' && (
                        <td style={{ padding: 8, whiteSpace: 'nowrap' }}>
                          {unitLines[index]?.purchaseOrderLineId ? (
                            <span style={{ color: Number(unitLines[index]?.quantity) > remainingOf(unitLines[index]?.purchaseOrderLineId) ? 'var(--color-error)' : undefined }}>
                              {remainingOf(unitLines[index]?.purchaseOrderLineId)}
                            </span>
                          ) : (
                            <span style={{ color: 'var(--color-text-muted)' }}>{t('purchaseInvoices.manualLine')}</span>
                          )}
                        </td>
                      )}
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`lines.${index}.unitPrice` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`lines.${index}.discountAmount` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field }) => (
                            <UnitSelect
                              item={unitItems.get(Number(unitLines[index]?.itemId))}
                              value={field.value || defaultUnitId(unitItems.get(Number(unitLines[index]?.itemId)))}
                              onChange={field.onChange}
                              disabled={!isEditable}
                              width={140}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 90 }} {...register(`lines.${index}.weight` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 90 }} {...register(`lines.${index}.allocationPercentage` as const, { valueAsNumber: true })} />
                      </td>
                      {!isNew && (
                        <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>{invoice?.lines[index]?.receivedQuantity ?? 0}</td>
                      )}
                      {!isNew && (
                        <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>{invoice?.lines[index]?.allocatedAdditionalCost?.toFixed(2) ?? '0.00'}</td>
                      )}
                      {isEditable && (
                        <td>
                          <Button type="button" variant="ghost" onClick={() => remove(index)}>{t('common.remove')}</Button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {isEditable && (
              <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                {/* Remarks6: a manual line is only offered when the company allows one on an ordered invoice. */}
                {(allowManualLines || watchedOrderId === '') && (
                  <Button type="button" variant="ghost" onClick={() => append(emptyLine())}>
                    {t('purchaseInvoices.addLine')}
                  </Button>
                )}
                {watchedOrderId !== '' && (orderLines?.length ?? 0) > 0 && (
                  <Button type="button" onClick={loadOrderLines}>
                    {t('purchaseInvoices.loadOrderLines')}
                  </Button>
                )}
              </div>
            )}

            {invoice && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('purchaseInvoices.subtotal')}: {invoice.subtotal.toFixed(2)}</span>
                <span>{t('purchaseInvoices.totalAmount')}: {invoice.totalAmount.toFixed(2)} {invoice.currencyCode}</span>
                {invoice.amountPaid > 0 && (
                  <span>{t('purchaseInvoices.amountPaid')}: {invoice.amountPaid.toFixed(2)} {invoice.currencyCode}</span>
                )}
                {invoice.journalEntryId && (
                  <span>
                    {t('purchaseInvoices.journalEntry')}:{' '}
                    <Link to={`/accounting/journal-entries/${invoice.journalEntryId}`}>{invoice.journalEntryNumber}</Link>
                  </span>
                )}
                {invoice.reversalJournalEntryId && (
                  <span>
                    {t('purchaseInvoices.reversalEntry')}:{' '}
                    <Link to={`/accounting/journal-entries/${invoice.reversalJournalEntryId}`}>{invoice.reversalJournalEntryNumber}</Link>
                  </span>
                )}
              </div>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('purchaseInvoices.notes'))}>
                <textarea
                  disabled={!isEditable}
                  rows={3}
                  style={{
                    width: '100%', padding: '9px 12px', borderRadius: 'var(--radius-chip)', border: '1px solid transparent',
                    background: 'var(--color-surface-2)', fontSize: 14, fontFamily: 'inherit', resize: 'vertical', boxSizing: 'border-box'
                  }}
                  {...register('notes')}
                />
              </FieldWrapper>
            </div>
          </CardBody>
        </Card>
      </form>

      {!isNew && <PurchaseInvoiceSourceOrderPanel invoiceId={invoiceId} />}

      {showAttachments && !isNew && <AttachmentPanel entityType="PurchaseInvoice" entityId={invoiceId} />}
    </div>
  );
}
