import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
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
import { useSuppliersList } from '../suppliers/api';
import { useWarehousesList } from '../../inventory/warehouses/api';
import { useItemsList } from '../../inventory/items/api';
import { UnitSelect, defaultUnitId } from '../../inventory/items/UnitSelect';
import { usePurchaseInvoicesList } from '../purchaseInvoices/api';
import {
  useCancelPurchaseReturn, useCreatePurchaseReturn, useInvoiceLinesForReturn, usePostPurchaseReturn, usePurchaseReturn,
  useUpdatePurchaseReturn
} from './api';
import type { PurchaseReturnLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const REASONS = ['Damaged', 'Expired', 'WrongItem', 'WrongQuantity', 'PriceMismatch', 'QualityIssue', 'Other'] as const;

interface FormValues {
  supplierId: number | '';
  purchaseInvoiceId: number | '';
  warehouseId: number | '';
  returnDate: string;
  reason: (typeof REASONS)[number];
  notes: string;
  lines: PurchaseReturnLineInput[];
}

const emptyLine = (): PurchaseReturnLineInput => ({ itemId: 0, quantity: 0, unitCost: 0, unitId: undefined });

/** /purchasing/purchase-returns/:id — screen #7. Draft → Posted decreases WarehouseId's stock
 * (rule 7); no accounting entry (IPostingService deferred, same as every Purchasing document). */
export function PurchaseReturnEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const returnId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: purchaseReturn, isLoading } = usePurchaseReturn(returnId);
  const { data: suppliers } = useSuppliersList();
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();
  const { data: invoicesPage } = usePurchaseInvoicesList({ page: 1, pageSize: 200 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_RETURN');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_PURCHASE_RETURN', 'PurchaseReturn', returnId);

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      supplierId: '', purchaseInvoiceId: '', warehouseId: '', returnDate: todayLocal(),
      reason: 'Damaged', notes: '', lines: [emptyLine()]
    }
  });
  const { fields, append, remove, replace } = useFieldArray({ control, name: 'lines' });
  // Each line's unit is one of its item's units (Fixes-Batch-2026-09-19).
  const unitLines = watch('lines');
  const unitItems = new Map((items ?? []).map((i) => [i.id, i]));

  useEffect(() => {
    if (purchaseReturn) {
      reset({
        supplierId: purchaseReturn.supplierId,
        purchaseInvoiceId: purchaseReturn.purchaseInvoiceId ?? '',
        warehouseId: purchaseReturn.warehouseId,
        returnDate: purchaseReturn.returnDate,
        reason: purchaseReturn.reason as (typeof REASONS)[number],
        notes: purchaseReturn.notes ?? '',
        lines: purchaseReturn.lines.map((l) => ({
          itemId: l.itemId, quantity: l.quantity, unitCost: l.unitCost, unitId: l.unitId, batchNumber: l.batchNumber,
          purchaseInvoiceLineId: l.purchaseInvoiceLineId
        }))
      });
    }
  }, [purchaseReturn, reset]);

  const createMutation = useCreatePurchaseReturn();
  const updateMutation = useUpdatePurchaseReturn(returnId ?? 0);
  const postMutation = usePostPurchaseReturn(returnId ?? 0);
  const cancelMutation = useCancelPurchaseReturn(returnId ?? 0);

  const isEditable = isNew || purchaseReturn?.status === 'Draft';
  const canPost = !isNew && purchaseReturn?.status === 'Draft';
  const canCancel = !isNew && purchaseReturn?.status === 'Draft';

  // Remarks4 item 8: a return that names an invoice may only send back that invoice's own lines,
  // capped at what is still returnable — so the item picker and the "add line" button go away and
  // the quantity box is bounded.
  const selectedInvoiceId = watch('purchaseInvoiceId');
  const { data: invoiceLines } = useInvoiceLinesForReturn(
    selectedInvoiceId === '' ? undefined : Number(selectedInvoiceId), returnId);
  const fromInvoice = selectedInvoiceId !== '' && (invoiceLines?.length ?? 0) > 0;

  // Lookups against the current invoice-line catalogue. All keyed by purchaseInvoiceLineId.
  const lineMeta = (invoiceLineId: number | undefined) =>
    (invoiceLines ?? []).find((l) => l.purchaseInvoiceLineId === invoiceLineId);
  const returnableOf = (invoiceLineId: number | undefined) => lineMeta(invoiceLineId)?.returnableQuantity ?? 0;
  const invoicedOf = (invoiceLineId: number | undefined) => lineMeta(invoiceLineId)?.invoicedQuantity ?? 0;
  const returnedOf = (invoiceLineId: number | undefined) => lineMeta(invoiceLineId)?.returnedQuantity ?? 0;
  const unitCodeOf = (invoiceLineId: number | undefined) => lineMeta(invoiceLineId)?.unitCode ?? '';

  // Auto-fill: once the invoice lines arrive, replace the pristine starting row with them, leaving
  // the return quantity at zero so the user types in the amount they actually want to send back.
  useEffect(() => {
    if (!isNew) return;
    if (!isEditable) return;
    if (selectedInvoiceId === '') return;
    if (!invoiceLines || invoiceLines.length === 0) return;

    const currentLines = watch('lines');
    const isPristine =
      currentLines.length === 1 &&
      !currentLines[0].purchaseInvoiceLineId &&
      !currentLines[0].itemId &&
      (!currentLines[0].quantity || currentLines[0].quantity === 0);

    if (!isPristine) return;

    const filled = invoiceLines
      .filter((l) => l.returnableQuantity > 0)
      .map((l) => ({
        itemId: l.itemId,
        purchaseInvoiceLineId: l.purchaseInvoiceLineId,
        quantity: 0,
        unitCost: l.unitCost,
        unitId: l.unitId,
        batchNumber: undefined
      }));

    if (filled.length > 0) {
      replace(filled);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoiceLines, selectedInvoiceId, isNew, isEditable, replace]);

  /** Fills the lines from the invoice — everything still returnable, which is the common case. */
  const takeWholeInvoice = () => {
    replace(
      (invoiceLines ?? [])
        .filter((l) => l.returnableQuantity > 0)
        .map((l) => ({
          itemId: l.itemId,
          purchaseInvoiceLineId: l.purchaseInvoiceLineId,
          quantity: l.returnableQuantity,
          unitCost: l.unitCost,
          unitId: l.unitId,
          batchNumber: undefined
        }))
    );
  };

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const invoiceOptions = (invoicesPage?.items ?? []).map((i) => ({ value: i.id, label: i.invoiceNumber }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

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
    const lines = values.lines
      .filter((l) => Number(l.quantity) > 0)
      .map((l) => ({
        itemId: Number(l.itemId), quantity: Number(l.quantity), unitCost: Number(l.unitCost),
        unitId: l.unitId ? Number(l.unitId) : undefined, batchNumber: l.batchNumber || undefined,
        purchaseInvoiceLineId: l.purchaseInvoiceLineId ? Number(l.purchaseInvoiceLineId) : undefined
      }));
    const common = {
      returnDate: values.returnDate,
      supplierId: Number(values.supplierId),
      purchaseInvoiceId: values.purchaseInvoiceId === '' ? undefined : Number(values.purchaseInvoiceId),
      warehouseId: Number(values.warehouseId),
      reason: values.reason,
      notes: values.notes || undefined,
      lines
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('purchaseReturns.createSuccess'), 'success');
        navigate(`/purchasing/purchase-returns/${newId}`);
      } else if (purchaseReturn) {
        await updateMutation.mutateAsync({ ...common, rowVersion: purchaseReturn.rowVersion });
        showToast(t('purchaseReturns.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('purchaseReturns.addPurchaseReturn') : `${t('purchaseReturns.title')} — ${purchaseReturn?.returnNumber ?? ''}`}</h2>
        {purchaseReturn && <StatusBadge status={purchaseReturn.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/purchase-returns') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(canPost ? [{ key: 'post', label: t('common.post'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => postMutation.mutateAsync(), t('purchaseReturns.postSuccess')); } }] : [])
        ]}
        destructive={
          canCancel
            ? [{ key: 'cancel', label: t('purchaseReturns.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('purchaseReturns.cancelSuccess')), confirmMessage: t('purchaseReturns.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('returnNumber', t('purchaseReturns.returnNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (purchaseReturn?.returnNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('supplier', t('purchaseReturns.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('purchaseInvoice', t('purchaseReturns.purchaseInvoice'))}>
                <Controller
                  control={control}
                  name="purchaseInvoiceId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 180 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={invoiceOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('warehouse', t('purchaseReturns.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('returnDate', t('purchaseReturns.returnDate'))}>
                <Input type="date" disabled={!isEditable} {...register('returnDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('reason', t('purchaseReturns.reason'))}>
                <Controller
                  control={control}
                  name="reason"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      style={{ minWidth: 160 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={REASONS.map((r) => ({ value: r, label: t(`purchaseReturns.reason${r}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('purchaseReturns.item'))}</th>
                    {fromInvoice && (
                      <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{t('purchaseReturns.invoicedQuantity')}</th>
                    )}
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('purchaseReturns.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitCost', t('purchaseReturns.unitCost'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('purchaseReturns.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('purchaseReturns.batchNumber'))}</th>
                    {isEditable && <th />}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>
                        {fromInvoice ? (
                          <Controller
                            control={control}
                            name={`lines.${index}.purchaseInvoiceLineId` as const}
                            render={({ field }) => (
                              <SearchableSelect
                                disabled={!isEditable}
                                style={{ width: 260 }}
                                value={field.value ?? ''}
                                onChange={(v) => {
                                  const invoiceLine = (invoiceLines ?? []).find((l) => l.purchaseInvoiceLineId === Number(v));
                                  field.onChange(v === '' ? undefined : Number(v));
                                  if (invoiceLine) {
                                    setValue(`lines.${index}.itemId`, invoiceLine.itemId);
                                    setValue(`lines.${index}.unitId`, invoiceLine.unitId);
                                    setValue(`lines.${index}.unitCost`, invoiceLine.unitCost);
                                    setValue(`lines.${index}.quantity`, 0);
                                  }
                                }}
                                options={(invoiceLines ?? []).map((l) => ({
                                  value: l.purchaseInvoiceLineId,
                                  label: `${l.itemCode} — ${l.itemNameAr} (${t('purchaseReturns.returnable')}: ${l.returnableQuantity} ${l.unitCode})`
                                }))}
                              />
                            )}
                          />
                        ) : (
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
                                options={[{ value: 0, label: t('purchaseReturns.selectItem') }, ...itemOptions]}
                              />
                            )}
                          />
                        )}
                      </td>
                      {fromInvoice && (
                        <td style={{ padding: 8, verticalAlign: 'top' }}>
                          <div
                            style={{
                              display: 'inline-block',
                              padding: '8px 12px',
                              background: 'var(--color-surface-3)',
                              borderRadius: 'var(--radius-chip)',
                              minWidth: 100,
                              textAlign: 'center',
                              fontWeight: 600
                            }}
                          >
                            {invoicedOf(unitLines[index]?.purchaseInvoiceLineId).toFixed(2)}
                            {unitCodeOf(unitLines[index]?.purchaseInvoiceLineId) && (
                              <span style={{ fontSize: 11, color: 'var(--color-text-muted)', marginInlineStart: 4 }}>
                                {unitCodeOf(unitLines[index]?.purchaseInvoiceLineId)}
                              </span>
                            )}
                          </div>
                          {returnedOf(unitLines[index]?.purchaseInvoiceLineId) > 0 && (
                            <div style={{ fontSize: 11, color: 'var(--color-warning)', marginTop: 2 }}>
                              {t('purchaseReturns.alreadyReturned')}: {returnedOf(unitLines[index]?.purchaseInvoiceLineId)}
                            </div>
                          )}
                        </td>
                      )}
                      <td style={{ padding: 8, verticalAlign: 'top' }}>
                        <Input
                          type="number"
                          step="0.0001"
                          disabled={!isEditable}
                          max={fromInvoice ? returnableOf(unitLines[index]?.purchaseInvoiceLineId) : undefined}
                          min={0}
                          style={{ width: 100 }}
                          {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })}
                        />
                        {fromInvoice && (
                          <div style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>
                            {t('purchaseReturns.returnable')}: {returnableOf(unitLines[index]?.purchaseInvoiceLineId)}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: 8, verticalAlign: 'top' }}>
                        <Input type="number" step="0.01" disabled={!isEditable || fromInvoice} style={{ width: 100 }} {...register(`lines.${index}.unitCost` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8, verticalAlign: 'top' }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field }) => (
                            <UnitSelect
                              item={unitItems.get(Number(unitLines[index]?.itemId))}
                              value={field.value || defaultUnitId(unitItems.get(Number(unitLines[index]?.itemId)))}
                              onChange={field.onChange}
                              disabled={!isEditable || fromInvoice}
                              width={140}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8, verticalAlign: 'top' }}>
                        <Input disabled={!isEditable || fromInvoice} style={{ width: 140 }} {...register(`lines.${index}.batchNumber` as const)} />
                      </td>
                      {isEditable && (
                        <td style={{ verticalAlign: 'top' }}>
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
                  {t('purchaseReturns.addLine')}
                </Button>
                {fromInvoice && (
                  <Button type="button" onClick={takeWholeInvoice}>
                    {t('purchaseReturns.returnWholeInvoice')}
                  </Button>
                )}
              </div>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('purchaseReturns.notes'))}>
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

      {showAttachments && !isNew && <AttachmentPanel entityType="PurchaseReturn" entityId={returnId} />}
    </div>
  );
}