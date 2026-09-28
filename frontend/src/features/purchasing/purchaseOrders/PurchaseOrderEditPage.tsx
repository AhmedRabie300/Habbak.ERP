import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { AttachmentPanel } from '../../../ui-kit/AttachmentPanel';
import { PurchaseOrderSourceRequestPanel } from '../common/SourceDocumentPanels';
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
import { useOpenPurchaseRequests, usePurchaseRequestLinesForOrder } from './requestLinkingApi';
import {
  useCancelPurchaseOrder, useConfirmPurchaseOrder, useCreatePurchaseOrder,
  usePurchaseOrder, useRejectPurchaseOrder, useSendPurchaseOrder, useUpdatePurchaseOrder
} from './api';
import type { PurchaseOrderLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const PAYMENT_TERMS = ['Cash', 'Net15', 'Net30', 'Net60'] as const;
const DELIVERY_TERMS = ['FOB', 'CIF', 'EXW'] as const;

interface FormValues {
  supplierId: number | '';
  purchaseRequestId: number | '';
  orderDate: string;
  currencyCode: string | '';
  exchangeRate: number;
  paymentTerms: (typeof PAYMENT_TERMS)[number];
  deliveryTerms: (typeof DELIVERY_TERMS)[number] | '';
  expectedDeliveryDate: string;
  deliveryAddress: string;
  taxAmount: number;
  discountAmount: string;
  discountReason: string;
  notes: string;
  lines: PurchaseOrderLineInput[];
}

const emptyLine = (): PurchaseOrderLineInput => ({ itemId: 0, quantity: 0, unitPrice: 0, unitId: undefined });

/** /purchasing/purchase-orders/:id — screen #4 (03-Module-Purchasing.md, section 8), the hub of
 * the purchase cycle. Draft → Sent → Confirmed (section 6.3) — editing locks after Confirmed
 * (rule 14). Linking an Approved purchase request (new orders only) converts it, mirroring
 * BranchRequest's own approve-creates-TransferOrder pattern. */
export function PurchaseOrderEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const orderId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: order, isLoading } = usePurchaseOrder(orderId);
  const { data: suppliers } = useSuppliersList();
  const { data: items } = useItemsList();
  const { data: currencies } = useCurrenciesList();
  const { label } = useFieldLabels('PURCHASING_PURCHASE_ORDER');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_PURCHASE_ORDER', 'PurchaseOrder', orderId);

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      supplierId: '', purchaseRequestId: '', orderDate: todayLocal(),
      currencyCode: '', exchangeRate: 1, paymentTerms: 'Net30', deliveryTerms: '', expectedDeliveryDate: '',
      deliveryAddress: '', taxAmount: 0, discountAmount: '', discountReason: '', notes: '', lines: [emptyLine()]
    }
  });
  // Remarks3 item 1: a new record starts in the default currency.
  useApplyDefaultCurrency(isNew, watch('currencyCode'), (code) => setValue('currencyCode', code), 'code');
  const { fields, append, remove, replace } = useFieldArray({ control, name: 'lines' });

  // Remarks7: which requests are on offer, and the chosen request fills the lines. Only relevant for a
  // new order — purchaseRequestId is fixed once the order exists (mirrors Remarks6's own order link).
  const watchedRequestId = watch('purchaseRequestId');
  const { data: openRequests } = useOpenPurchaseRequests();
  const { data: requestLines } = usePurchaseRequestLinesForOrder(
    isNew && watchedRequestId !== '' ? Number(watchedRequestId) : undefined
  );

  const remainingOf = (requestLineId: number | undefined) =>
    (requestLines ?? []).find((l) => l.purchaseRequestLineId === requestLineId)?.remainingQuantity ?? 0;

  /** Everything the chosen request still owes, as order lines the user can trim before saving. */
  const loadRequestLines = () => {
    replace(
      (requestLines ?? []).map((l) => ({
        itemId: l.itemId,
        purchaseRequestLineId: l.purchaseRequestLineId,
        quantity: l.remainingQuantity,
        unitPrice: l.unitPrice,
        unitId: l.unitId
      }))
    );
  };

  /** Picking a different request throws away what the previous one filled in — the lines belong to it. */
  const chooseRequest = (value: number | '') => {
    const hasLines = (watch('lines') ?? []).some((l) => Number(l.itemId) > 0);
    if (hasLines && !window.confirm(t('purchaseOrders.changeRequestConfirm'))) {
      return false;
    }

    setValue('purchaseRequestId', value);
    if (value === '') {
      replace([emptyLine()]);
    }

    return true;
  };

  /** Auto-loads the moment the chosen request's lines arrive — the "تحميل سطور الطلب" button stays
   * only as a manual reload for after the user has edited lines by hand. Guarded to isNew: an
   * existing order's purchaseRequestId is fixed (reset() loads its real saved lines, and
   * requestLines here would just be a fresh, wrong-to-apply recomputation of what is left). */
  const lastAutoLoadedRequestId = useRef<number | ''>('');
  useEffect(() => {
    if (!isNew) return;
    if (watchedRequestId === '') {
      lastAutoLoadedRequestId.current = '';
      return;
    }
    if (watchedRequestId === lastAutoLoadedRequestId.current || requestLines === undefined) {
      return;
    }

    lastAutoLoadedRequestId.current = watchedRequestId;
    loadRequestLines();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isNew, watchedRequestId, requestLines]);
  // Each line's unit is one of its item's units (Fixes-Batch-2026-09-19).
  const unitLines = watch('lines');
  const unitItems = new Map((items ?? []).map((i) => [i.id, i]));

  useEffect(() => {
    if (order) {
      reset({
        supplierId: order.supplierId,
        purchaseRequestId: order.purchaseRequestId ?? '',
        orderDate: order.orderDate,
        currencyCode: order.currencyCode,
        exchangeRate: order.exchangeRate,
        paymentTerms: order.paymentTerms as (typeof PAYMENT_TERMS)[number],
        deliveryTerms: (order.deliveryTerms as (typeof DELIVERY_TERMS)[number] | undefined) ?? '',
        expectedDeliveryDate: order.expectedDeliveryDate ?? '',
        deliveryAddress: order.deliveryAddress ?? '',
        taxAmount: order.taxAmount,
        discountAmount: order.discountAmount?.toString() ?? '',
        discountReason: order.discountReason ?? '',
        notes: order.notes ?? '',
        lines: order.lines.map((l) => ({
          itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discountAmount,
          unitId: l.unitId, expectedDeliveryDate: l.expectedDeliveryDate, weight: l.weight,
          purchaseRequestLineId: l.purchaseRequestLineId
        }))
      });
    }
  }, [order, reset]);

  const createMutation = useCreatePurchaseOrder();
  const updateMutation = useUpdatePurchaseOrder(orderId ?? 0);
  const sendMutation = useSendPurchaseOrder(orderId ?? 0);
  const confirmMutation = useConfirmPurchaseOrder(orderId ?? 0);
  const rejectMutation = useRejectPurchaseOrder(orderId ?? 0);
  const cancelMutation = useCancelPurchaseOrder(orderId ?? 0);

  const isEditable = isNew || order?.status === 'Draft' || order?.status === 'Sent';
  const isDraft = isNew || order?.status === 'Draft';
  const isSent = order?.status === 'Sent';
  const isCancellable = order?.status === 'Draft' || order?.status === 'Sent' || order?.status === 'Confirmed';

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const requestOptions = (openRequests ?? []).map((r) => ({
    value: r.id,
    label: `${r.requestNumber} — ${r.requestDate} — ${t('purchaseOrders.remainingLines', { count: r.remainingLineCount })}`
  }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const currencyOptions = (currencies ?? []).map((c) => ({ value: c.code, label: `${c.code} — ${c.nameAr}` }));

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
      itemId: Number(l.itemId), quantity: Number(l.quantity), unitPrice: Number(l.unitPrice),
      discountAmount: l.discountAmount ? Number(l.discountAmount) : undefined, unitId: l.unitId ? Number(l.unitId) : undefined,
      expectedDeliveryDate: l.expectedDeliveryDate || undefined, weight: l.weight ? Number(l.weight) : undefined,
      purchaseRequestLineId: l.purchaseRequestLineId ? Number(l.purchaseRequestLineId) : undefined
    }));
    const common = {
      orderDate: values.orderDate,
      supplierId: Number(values.supplierId),
      currencyCode: String(values.currencyCode),
      exchangeRate: Number(values.exchangeRate),
      paymentTerms: values.paymentTerms,
      deliveryTerms: values.deliveryTerms || undefined,
      expectedDeliveryDate: values.expectedDeliveryDate || undefined,
      deliveryAddress: values.deliveryAddress || undefined,
      taxAmount: Number(values.taxAmount),
      discountAmount: values.discountAmount ? Number(values.discountAmount) : undefined,
      discountReason: values.discountReason || undefined,
      notes: values.notes || undefined,
      lines
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          ...common,
          purchaseRequestId: values.purchaseRequestId === '' ? undefined : Number(values.purchaseRequestId)
        });
        showToast(t('purchaseOrders.createSuccess'), 'success');
        navigate(`/purchasing/purchase-orders/${newId}`);
      } else if (order) {
        await updateMutation.mutateAsync({ ...common, rowVersion: order.rowVersion });
        showToast(t('purchaseOrders.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('purchaseOrders.addPurchaseOrder') : `${t('purchaseOrders.title')} — ${order?.orderNumber ?? ''}`}</h2>
        {order && <StatusBadge status={order.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/purchase-orders') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(isDraft && !isNew ? [{ key: 'send', buttonCode: 'Send', label: t('purchaseOrders.send'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => sendMutation.mutateAsync(), t('purchaseOrders.sendSuccess')); } }] : []),
          ...(isSent ? [{ key: 'confirm', buttonCode: 'Confirm', label: t('purchaseOrders.confirm'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => confirmMutation.mutateAsync(), t('purchaseOrders.confirmSuccess')); } }] : [])
        ]}
        destructive={[
          ...(isDraft || isSent ? [{ key: 'reject', buttonCode: 'Reject', label: t('purchaseOrders.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('purchaseOrders.rejectSuccess')), confirmMessage: t('purchaseOrders.rejectConfirm') }] : []),
          ...(isCancellable ? [{ key: 'cancel', buttonCode: 'Cancel', label: t('purchaseOrders.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('purchaseOrders.cancelSuccess')), confirmMessage: t('purchaseOrders.cancelConfirm') }] : [])
        ]}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('orderNumber', t('purchaseOrders.orderNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (order?.orderNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('supplier', t('purchaseOrders.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              {isNew && (
                <FieldWrapper label={label('purchaseRequest', t('purchaseOrders.requestLabel'))}>
                  <Controller
                    control={control}
                    name="purchaseRequestId"
                    render={({ field }) => (
                      <SearchableSelect
                        style={{ minWidth: 320 }}
                        value={field.value}
                        onChange={(v) => chooseRequest(v === '' ? '' : Number(v))}
                        options={[{ value: '', label: t('purchaseOrders.noPurchaseRequest') }, ...requestOptions]}
                      />
                    )}
                  />
                </FieldWrapper>
              )}
              {!isNew && order?.purchaseRequestNumber && (
                <FieldWrapper label={label('purchaseRequest', t('purchaseOrders.purchaseRequest'))}>
                  <Input value={order.purchaseRequestNumber} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('orderDate', t('purchaseOrders.orderDate'))}>
                <Input type="date" disabled={!isEditable} {...register('orderDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('currency', t('purchaseOrders.currency'))}>
                <Controller
                  control={control}
                  name="currencyCode"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 140 }} value={field.value} onChange={(v) => field.onChange(v)} options={currencyOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('exchangeRate', t('purchaseOrders.exchangeRate'))}>
                <Input type="number" step="0.000001" disabled={!isEditable} style={{ width: 120 }} {...register('exchangeRate', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('paymentTerms', t('purchaseOrders.paymentTerms'))}>
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
              <FieldWrapper label={label('deliveryTerms', t('purchaseOrders.deliveryTerms'))}>
                <Controller
                  control={control}
                  name="deliveryTerms"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      style={{ minWidth: 120 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={[{ value: '', label: '—' }, ...DELIVERY_TERMS.map((dt) => ({ value: dt, label: dt }))]}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('expectedDeliveryDate', t('purchaseOrders.expectedDeliveryDate'))}>
                <Input type="date" disabled={!isEditable} {...register('expectedDeliveryDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('deliveryAddress', t('purchaseOrders.deliveryAddress'))}>
                <Input disabled={!isEditable} style={{ minWidth: 220 }} {...register('deliveryAddress')} />
              </FieldWrapper>
              <FieldWrapper label={label('taxAmount', t('purchaseOrders.taxAmount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('taxAmount', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('discountAmount', t('purchaseOrders.discountAmount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 120 }} {...register('discountAmount')} />
              </FieldWrapper>
              <FieldWrapper label={label('discountReason', t('purchaseOrders.discountReason'))}>
                <Input disabled={!isEditable} style={{ minWidth: 180 }} {...register('discountReason')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('purchaseOrders.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('purchaseOrders.quantity'))}</th>
                    {isNew && watchedRequestId !== '' && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{t('purchaseOrders.remainingInRequest')}</th>}
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('purchaseOrders.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineDiscount', t('purchaseOrders.lineDiscount'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('purchaseOrders.unit'))}</th>
                    {!isNew && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('receivedQuantity', t('purchaseOrders.receivedQuantity'))}</th>}
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
                              options={[{ value: 0, label: t('purchaseOrders.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input
                          type="number"
                          step="0.0001"
                          disabled={!isEditable}
                          title={
                            unitLines[index]?.purchaseRequestLineId
                              && Number(unitLines[index]?.quantity) > remainingOf(unitLines[index]?.purchaseRequestLineId)
                              ? t('purchaseOrders.overRequestWarning')
                              : undefined
                          }
                          style={{ width: 100 }}
                          {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })}
                        />
                      </td>
                      {isNew && watchedRequestId !== '' && (
                        <td style={{ padding: 8, whiteSpace: 'nowrap' }}>
                          {unitLines[index]?.purchaseRequestLineId ? (
                            <span style={{ color: Number(unitLines[index]?.quantity) > remainingOf(unitLines[index]?.purchaseRequestLineId) ? 'var(--color-error)' : undefined }}>
                              {remainingOf(unitLines[index]?.purchaseRequestLineId)}
                            </span>
                          ) : (
                            <span style={{ color: 'var(--color-text-muted)' }}>—</span>
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
                      {!isNew && (
                        <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>{order?.lines[index]?.receivedQuantity ?? 0}</td>
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
                <Button type="button" variant="ghost" onClick={() => append(emptyLine())}>
                  {t('purchaseOrders.addLine')}
                </Button>
                {isNew && watchedRequestId !== '' && (requestLines?.length ?? 0) > 0 && (
                  <Button type="button" onClick={loadRequestLines}>
                    {t('purchaseOrders.loadRequestLines')}
                  </Button>
                )}
              </div>
            )}

            {order && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('purchaseOrders.subtotal')}: {order.subtotal.toFixed(2)}</span>
                <span>{t('purchaseOrders.totalAmount')}: {order.totalAmount.toFixed(2)} {order.currencyCode}</span>
              </div>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('purchaseOrders.notes'))}>
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

      {!isNew && <PurchaseOrderSourceRequestPanel orderId={orderId} />}

      {showAttachments && !isNew && <AttachmentPanel entityType="PurchaseOrder" entityId={orderId} />}
    </div>
  );
}
