import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { AttachmentPanel } from '../../../ui-kit/AttachmentPanel';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useMandatorySettings } from '../../settings/codingRules/useMandatorySettings';
import { useWarehousesList } from '../../inventory/warehouses/api';
import { useItemsList } from '../../inventory/items/api';
import { UnitSelect } from '../../inventory/items/UnitSelect';
import { useCreateGoodsReceipt, useGoodsReceipt, usePostGoodsReceipt, usePostablePurchaseOrdersList, useCancelGoodsReceipt } from './api';
import type { GoodsReceiptLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const QUALITY_STATUSES = ['Pending', 'Passed', 'PartiallyPassed', 'Failed'] as const;

interface LineFormValues extends GoodsReceiptLineInput {
  itemCode?: string;
  itemNameAr?: string;
  unitCode?: string;
}

interface FormValues {
  warehouseId: number | '';
  receiptDate: string;
  purchaseOrderId: number | '';
  notes: string;
  lines: LineFormValues[];
}

/** /purchasing/goods-receipts/:id — screen #6. New receipts are always created from a
 * Confirmed/PartiallyReceived purchase order (picking it pre-fills the outstanding lines);
 * once created there's no line-editing, only Post (moves stock, rule PUR-RECEIPT) or Cancel. */
export function GoodsReceiptEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const receiptId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: receipt, isLoading } = useGoodsReceipt(receiptId);
  const { data: warehouses } = useWarehousesList();
  // The unit each line is received in — the order line's unit to start with, or another of the item's units.
  const { data: items } = useItemsList();
  const { data: postableOrders } = usePostablePurchaseOrdersList();
  const { label } = useFieldLabels('PURCHASING_GOODS_RECEIPT');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_GOODS_RECEIPT', 'GoodsReceipt', receiptId);

  const { register, control, handleSubmit, reset, watch, setValue } = useForm<FormValues>({
    defaultValues: { warehouseId: '', receiptDate: todayLocal(), purchaseOrderId: '', notes: '', lines: [] }
  });
  const { fields } = useFieldArray({ control, name: 'lines' });
  const selectedOrderId = watch('purchaseOrderId');

  useEffect(() => {
    if (receipt) {
      reset({
        warehouseId: receipt.warehouseId,
        receiptDate: receipt.receiptDate,
        purchaseOrderId: receipt.purchaseOrderId,
        notes: receipt.notes ?? '',
        lines: receipt.lines.map((l) => ({
          itemId: l.itemId, itemCode: l.itemCode, itemNameAr: l.itemNameAr,
          quantity: l.quantity, acceptedQuantity: l.acceptedQuantity, rejectedQuantity: l.rejectedQuantity,
          rejectedReason: l.rejectedReason, rejectedWarehouseId: l.rejectedWarehouseId, unitCost: l.unitCost,
          unitId: l.unitId, unitCode: l.unitCode, varianceReason: l.varianceReason, batchNumber: l.batchNumber,
          expiryDate: l.expiryDate, qualityCheckStatus: l.qualityCheckStatus, qualityCheckNotes: l.qualityCheckNotes
        }))
      });
    }
  }, [receipt, reset]);

  // New receipt: selecting a postable order pre-fills its outstanding lines.
  useEffect(() => {
    if (isNew && selectedOrderId !== '') {
      const order = (postableOrders ?? []).find((o) => o.id === Number(selectedOrderId));
      if (order) {
        setValue('lines', order.lines.map((l) => ({
          itemId: l.itemId, itemCode: l.itemCode, itemNameAr: l.itemNameAr,
          quantity: l.remainingQuantity, acceptedQuantity: l.remainingQuantity, rejectedQuantity: 0,
          unitCost: 0, unitId: l.unitId, unitCode: l.unitCode, qualityCheckStatus: 'Pending'
        })));
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isNew, selectedOrderId, postableOrders]);

  const createMutation = useCreateGoodsReceipt();
  const postMutation = usePostGoodsReceipt(receiptId ?? 0);
  const cancelMutation = useCancelGoodsReceipt(receiptId ?? 0);

  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const orderOptions = (postableOrders ?? []).map((o) => ({ value: o.id, label: `${o.orderNumber} — ${o.supplierCode}` }));
  const damagedWarehouseOptions = (warehouses ?? []).filter((w) => w.warehouseType === 'DamagedReturns').map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));

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
      itemId: Number(l.itemId), quantity: Number(l.quantity), acceptedQuantity: Number(l.acceptedQuantity),
      rejectedQuantity: Number(l.rejectedQuantity), rejectedReason: l.rejectedReason || undefined,
      rejectedWarehouseId: l.rejectedWarehouseId ? Number(l.rejectedWarehouseId) : undefined,
      unitCost: Number(l.unitCost), unitId: l.unitId ? Number(l.unitId) : undefined, varianceReason: l.varianceReason || undefined,
      batchNumber: l.batchNumber || undefined, expiryDate: l.expiryDate || undefined,
      qualityCheckStatus: l.qualityCheckStatus, qualityCheckNotes: l.qualityCheckNotes || undefined
    }));

    try {
      const { id: newId } = await createMutation.mutateAsync({
        warehouseId: Number(values.warehouseId),
        receiptDate: values.receiptDate,
        purchaseOrderId: Number(values.purchaseOrderId),
        notes: values.notes || undefined,
        lines
      });
      showToast(t('goodsReceipts.createSuccess'), 'success');
      navigate(`/purchasing/goods-receipts/${newId}`);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  const isDraft = isNew || receipt?.status === 'Draft';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('goodsReceipts.addGoodsReceipt') : `${t('goodsReceipts.title')} — ${receipt?.receiptNumber ?? ''}`}</h2>
        {receipt && <StatusBadge status={receipt.status} />}
      </div>

      <ActionBar
        primary={isNew ? { key: 'save', label: t('common.saveDraft'), onClick: () => onSave() } : receipt?.status === 'Draft' ? { key: 'post', label: t('common.post'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => postMutation.mutateAsync(), t('goodsReceipts.postSuccess')); } } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/goods-receipts') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : [])
        ]}
        destructive={receipt?.status === 'Draft' ? [{ key: 'cancel', label: t('goodsReceipts.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('goodsReceipts.cancelSuccess')), confirmMessage: t('goodsReceipts.cancelConfirm') }] : []}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('receiptNumber', t('goodsReceipts.receiptNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (receipt?.receiptNumber ?? '')} disabled />
              </FieldWrapper>
              {isNew ? (
                <FieldWrapper label={label('purchaseOrder', t('goodsReceipts.purchaseOrder'))}>
                  <Controller
                    control={control}
                    name="purchaseOrderId"
                    render={({ field }) => (
                      <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={orderOptions} />
                    )}
                  />
                </FieldWrapper>
              ) : (
                <FieldWrapper label={label('purchaseOrder', t('goodsReceipts.purchaseOrder'))}>
                  <Input value={receipt?.purchaseOrderNumber ?? ''} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('warehouse', t('goodsReceipts.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('receiptDate', t('goodsReceipts.receiptDate'))}>
                <Input type="date" disabled={!isDraft} {...register('receiptDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('goodsReceipts.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('goodsReceipts.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('goodsReceipts.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('acceptedQuantity', t('goodsReceipts.acceptedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('rejectedQuantity', t('goodsReceipts.rejectedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('rejectedReason', t('goodsReceipts.rejectedReason'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('rejectedWarehouse', t('goodsReceipts.rejectedWarehouse'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitCost', t('goodsReceipts.unitCost'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('goodsReceipts.batchNumber'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('varianceReason', t('goodsReceipts.varianceReason'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('qualityCheckStatus', t('goodsReceipts.qualityCheckStatus'))}</th>
                    {!isNew && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('variance', t('goodsReceipts.variance'))}</th>}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>{field.itemCode} — {field.itemNameAr}</td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field: unitField }) => (
                            <UnitSelect
                              item={items?.find((i) => i.id === field.itemId)}
                              value={unitField.value}
                              onChange={unitField.onChange}
                              disabled={!isDraft}
                              width={130}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 90 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 90 }} {...register(`lines.${index}.acceptedQuantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 90 }} {...register(`lines.${index}.rejectedQuantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isDraft} style={{ width: 150 }} {...register(`lines.${index}.rejectedReason` as const)} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.rejectedWarehouseId` as const}
                          render={({ field }) => (
                            <SearchableSelect disabled={!isDraft} style={{ width: 160 }} value={field.value ?? ''} onChange={(v) => field.onChange(v === '' ? undefined : Number(v))} options={damagedWarehouseOptions} />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isDraft} style={{ width: 100 }} {...register(`lines.${index}.unitCost` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isDraft} style={{ width: 120 }} {...register(`lines.${index}.batchNumber` as const)} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isDraft} style={{ width: 150 }} {...register(`lines.${index}.varianceReason` as const)} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.qualityCheckStatus` as const}
                          render={({ field }) => (
                            <SearchableSelect
                              disabled={!isDraft}
                              style={{ width: 140 }}
                              value={field.value}
                              onChange={(v) => field.onChange(v)}
                              options={QUALITY_STATUSES.map((qs) => ({ value: qs, label: t(`goodsReceipts.quality${qs}`) }))}
                            />
                          )}
                        />
                      </td>
                      {!isNew && <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>{receipt?.lines[index]?.varianceQuantity ?? 0}</td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('goodsReceipts.notes'))}>
                <textarea
                  disabled={!isDraft}
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

      {showAttachments && !isNew && <AttachmentPanel entityType="GoodsReceipt" entityId={receiptId} />}
    </div>
  );
}
