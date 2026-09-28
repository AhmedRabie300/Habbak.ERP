import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useWarehousesList } from '../../inventory/warehouses/api';
import { useItemsList } from '../../inventory/items/api';
import { useConfirmedSalesOrdersList, useSalesOrder } from '../salesOrders/api';
import { usePostedSalesInvoicesList, useSalesInvoice } from '../salesInvoices/api';
import {
  useCreateDeliveryOrder, useDeliveryOrder, usePostDeliveryOrder, useRejectDeliveryOrder, useUpdateDeliveryOrder
} from './api';
import type { DeliveryOrderLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  customerId: number | '';
  warehouseId: number | '';
  deliveryDate: string;
  sourceOrderId: number | '';
  sourceInvoiceId: number | '';
  lines: DeliveryOrderLineInput[];
}

const emptyLine = (): DeliveryOrderLineInput => ({ itemId: 0, quantity: 0 });

/** /sales/delivery-orders/:id — screen #8 (04-Module-Sales.md, section 5). Exactly one of a
 * confirmed order or a posted invoice must be picked (قاعدة 31) — picking either auto-fills the
 * customer and lines (remaining quantity for an order, full quantity for an invoice). Posting is
 * where StockBalance actually moves (قاعدة 17). */
export function DeliveryOrderEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const deliveryOrderId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: deliveryOrder, isLoading } = useDeliveryOrder(deliveryOrderId);
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();
  const { data: confirmedOrders } = useConfirmedSalesOrdersList();
  const { data: postedInvoices } = usePostedSalesInvoicesList();
  const { label } = useFieldLabels('SALES_DELIVERY_ORDER');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: { customerId: '', warehouseId: '', deliveryDate: todayLocal(), sourceOrderId: '', sourceInvoiceId: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const selectedSourceOrderId = watch('sourceOrderId');
  const selectedSourceInvoiceId = watch('sourceInvoiceId');
  const { data: selectedOrder } = useSalesOrder(isNew && selectedSourceOrderId ? Number(selectedSourceOrderId) : undefined);
  const { data: selectedInvoice } = useSalesInvoice(isNew && selectedSourceInvoiceId ? Number(selectedSourceInvoiceId) : undefined);

  useEffect(() => {
    if (selectedOrder && isNew) {
      setValue('customerId', selectedOrder.customerId);
      setValue('sourceInvoiceId', '');
      const remaining = selectedOrder.lines
        .map((l) => ({ itemId: l.itemId, quantity: l.quantity - l.deliveredQuantity }))
        .filter((l) => l.quantity > 0);
      setValue('lines', remaining.length ? remaining : [emptyLine()]);
    }
  }, [selectedOrder, isNew, setValue]);

  useEffect(() => {
    if (selectedInvoice && isNew) {
      setValue('customerId', selectedInvoice.customerId);
      setValue('sourceOrderId', '');
      setValue('lines', selectedInvoice.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity })));
    }
  }, [selectedInvoice, isNew, setValue]);

  useEffect(() => {
    if (deliveryOrder) {
      reset({
        customerId: deliveryOrder.customerId,
        warehouseId: deliveryOrder.warehouseId,
        deliveryDate: deliveryOrder.deliveryDate,
        sourceOrderId: deliveryOrder.sourceOrderId ?? '',
        sourceInvoiceId: deliveryOrder.sourceInvoiceId ?? '',
        lines: deliveryOrder.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, batchNumber: l.batchNumber }))
      });
    }
  }, [deliveryOrder, reset]);

  const createMutation = useCreateDeliveryOrder();
  const updateMutation = useUpdateDeliveryOrder(deliveryOrderId ?? 0);
  const postMutation = usePostDeliveryOrder(deliveryOrderId ?? 0);
  const rejectMutation = useRejectDeliveryOrder(deliveryOrderId ?? 0);

  const isDraft = isNew || deliveryOrder?.status === 'Draft';

  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const orderOptions = (confirmedOrders ?? []).map((o) => ({ value: o.id, label: `${o.orderNumber} — ${o.customerNameAr}` }));
  const invoiceOptions = (postedInvoices ?? []).map((i) => ({ value: i.id, label: `${i.invoiceNumber} — ${i.customerNameAr}` }));

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
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), quantity: Number(l.quantity), batchNumber: l.batchNumber || undefined }));

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          customerId: Number(values.customerId),
          warehouseId: Number(values.warehouseId),
          deliveryDate: values.deliveryDate,
          sourceOrderId: values.sourceOrderId === '' ? undefined : Number(values.sourceOrderId),
          sourceInvoiceId: values.sourceInvoiceId === '' ? undefined : Number(values.sourceInvoiceId),
          lines
        });
        showToast(t('deliveryOrders.createSuccess'), 'success');
        navigate(`/sales/delivery-orders/${newId}`);
      } else if (deliveryOrder) {
        await updateMutation.mutateAsync({
          warehouseId: Number(values.warehouseId),
          deliveryDate: values.deliveryDate,
          lines,
          rowVersion: deliveryOrder.rowVersion
        });
        showToast(t('deliveryOrders.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('deliveryOrders.addDeliveryOrder') : `${t('deliveryOrders.title')} — ${deliveryOrder?.deliveryNumber ?? ''}`}</h2>
        {deliveryOrder && <StatusBadge status={deliveryOrder.status} />}
      </div>

      <ActionBar
        primary={isDraft ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/sales/delivery-orders') },
          ...(isDraft && !isNew ? [{ key: 'post', label: t('deliveryOrders.post'), onClick: () => runAction(() => postMutation.mutateAsync(), t('deliveryOrders.postSuccess')) }] : [])
        ]}
        destructive={
          isDraft && !isNew
            ? [{ key: 'reject', label: t('deliveryOrders.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('deliveryOrders.rejectSuccess')), confirmMessage: t('deliveryOrders.rejectConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('deliveryNumber', t('deliveryOrders.deliveryNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (deliveryOrder?.deliveryNumber ?? '')} disabled />
              </FieldWrapper>
              {isNew && (
                <>
                  <FieldWrapper label={label('sourceOrder', t('deliveryOrders.sourceOrder'))}>
                    <Controller
                      control={control}
                      name="sourceOrderId"
                      render={({ field }) => (
                        <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={orderOptions} />
                      )}
                    />
                  </FieldWrapper>
                  <FieldWrapper label={label('sourceInvoice', t('deliveryOrders.sourceInvoice'))}>
                    <Controller
                      control={control}
                      name="sourceInvoiceId"
                      render={({ field }) => (
                        <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={invoiceOptions} />
                      )}
                    />
                  </FieldWrapper>
                </>
              )}
              {!isNew && deliveryOrder?.sourceOrderNumber && (
                <FieldWrapper label={label('sourceOrder', t('deliveryOrders.sourceOrder'))}>
                  <Input value={deliveryOrder.sourceOrderNumber} disabled />
                </FieldWrapper>
              )}
              {!isNew && deliveryOrder?.sourceInvoiceNumber && (
                <FieldWrapper label={label('sourceInvoice', t('deliveryOrders.sourceInvoice'))}>
                  <Input value={deliveryOrder.sourceInvoiceNumber} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('customer', t('deliveryOrders.customer'))}>
                <Input value={!isNew ? (deliveryOrder?.customerNameAr ?? '') : (confirmedOrders?.find((o) => o.id === Number(selectedSourceOrderId))?.customerNameAr ?? postedInvoices?.find((i) => i.id === Number(selectedSourceInvoiceId))?.customerNameAr ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('warehouse', t('deliveryOrders.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('deliveryDate', t('deliveryOrders.deliveryDate'))}>
                <Input type="date" disabled={!isDraft} {...register('deliveryDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('deliveryOrders.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('deliveryOrders.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('deliveryOrders.batchNumber'))}</th>
                    {isDraft && <th />}
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
                              disabled={!isDraft}
                              style={{ width: 220 }}
                              value={field.value}
                              onChange={(v) => field.onChange(Number(v))}
                              options={[{ value: 0, label: t('deliveryOrders.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 100 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isDraft} style={{ width: 140 }} {...register(`lines.${index}.batchNumber` as const)} />
                      </td>
                      {isDraft && (
                        <td>
                          <Button type="button" variant="ghost" onClick={() => remove(index)}>{t('common.remove')}</Button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {isDraft && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('deliveryOrders.addLine')}
              </Button>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
