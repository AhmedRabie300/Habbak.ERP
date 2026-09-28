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
import { useCustomersList } from '../customers/api';
import { useItemsList } from '../../inventory/items/api';
import { useAcceptedSalesQuotesList, useSalesQuote } from '../salesQuotes/api';
import {
  useCancelSalesOrder, useConfirmSalesOrder, useCreateSalesOrder, useRejectSalesOrder, useSalesOrder, useUpdateSalesOrder
} from './api';
import type { SalesOrderLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  customerId: number | '';
  orderDate: string;
  sourceQuoteId: number | '';
  lines: SalesOrderLineInput[];
}

const emptyLine = (): SalesOrderLineInput => ({ itemId: 0, quantity: 0, unitPrice: 0 });

/** /sales/sales-orders/:id — screen #6 (04-Module-Sales.md, section 5). Draft → Confirmed (section
 * 4.2) — editing locks after Draft. Picking an accepted quote (new orders only) copies its lines
 * automatically (قاعدة 15); CreateSalesOrderCommand re-validates the quote itself regardless of
 * what the frontend sends. */
export function SalesOrderEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const orderId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: order, isLoading } = useSalesOrder(orderId);
  const { data: customers } = useCustomersList();
  const { data: items } = useItemsList();
  const { data: acceptedQuotes } = useAcceptedSalesQuotesList();
  const { label } = useFieldLabels('SALES_ORDER');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: { customerId: '', orderDate: todayLocal(), sourceQuoteId: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const selectedQuoteId = watch('sourceQuoteId');
  const { data: selectedQuote } = useSalesQuote(
    isNew && selectedQuoteId ? Number(selectedQuoteId) : undefined
  );

  useEffect(() => {
    if (selectedQuote && isNew) {
      setValue('customerId', selectedQuote.customerId);
      setValue('lines', selectedQuote.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice })));
    }
  }, [selectedQuote, isNew, setValue]);

  useEffect(() => {
    if (order) {
      reset({
        customerId: order.customerId,
        orderDate: order.orderDate,
        sourceQuoteId: order.sourceQuoteId ?? '',
        lines: order.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice }))
      });
    }
  }, [order, reset]);

  const createMutation = useCreateSalesOrder();
  const updateMutation = useUpdateSalesOrder(orderId ?? 0);
  const confirmMutation = useConfirmSalesOrder(orderId ?? 0);
  const rejectMutation = useRejectSalesOrder(orderId ?? 0);
  const cancelMutation = useCancelSalesOrder(orderId ?? 0);

  const isDraft = isNew || order?.status === 'Draft';
  const isCancellable = order?.status === 'Draft' || order?.status === 'Confirmed';

  const customerOptions = (customers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const quoteOptions = (acceptedQuotes ?? []).map((q) => ({ value: q.id, label: `${q.quoteNumber} — ${q.customerNameAr}` }));

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
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), quantity: Number(l.quantity), unitPrice: Number(l.unitPrice) }));
    const common = { customerId: Number(values.customerId), orderDate: values.orderDate, lines };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          ...common,
          sourceQuoteId: values.sourceQuoteId === '' ? undefined : Number(values.sourceQuoteId)
        });
        showToast(t('salesOrders.createSuccess'), 'success');
        navigate(`/sales/sales-orders/${newId}`);
      } else if (order) {
        await updateMutation.mutateAsync({ ...common, rowVersion: order.rowVersion });
        showToast(t('salesOrders.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('salesOrders.addOrder') : `${t('salesOrders.title')} — ${order?.orderNumber ?? ''}`}</h2>
        {order && <StatusBadge status={order.status} />}
      </div>

      <ActionBar
        primary={isDraft ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/sales/sales-orders') },
          ...(isDraft && !isNew ? [{ key: 'confirm', label: t('salesOrders.confirm'), onClick: () => runAction(() => confirmMutation.mutateAsync(), t('salesOrders.confirmSuccess')) }] : [])
        ]}
        destructive={[
          ...(isDraft && !isNew ? [{ key: 'reject', label: t('salesOrders.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('salesOrders.rejectSuccess')), confirmMessage: t('salesOrders.rejectConfirm') }] : []),
          ...(isCancellable ? [{ key: 'cancel', label: t('salesOrders.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('salesOrders.cancelSuccess')), confirmMessage: t('salesOrders.cancelConfirm') }] : [])
        ]}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('orderNumber', t('salesOrders.orderNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (order?.orderNumber ?? '')} disabled />
              </FieldWrapper>
              {isNew && (
                <FieldWrapper label={label('sourceQuote', t('salesOrders.sourceQuote'))}>
                  <Controller
                    control={control}
                    name="sourceQuoteId"
                    render={({ field }) => (
                      <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={quoteOptions} />
                    )}
                  />
                </FieldWrapper>
              )}
              {!isNew && order?.sourceQuoteNumber && (
                <FieldWrapper label={label('sourceQuote', t('salesOrders.sourceQuote'))}>
                  <Input value={order.sourceQuoteNumber} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('customer', t('salesOrders.customer'))}>
                <Controller
                  control={control}
                  name="customerId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={customerOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('orderDate', t('salesOrders.orderDate'))}>
                <Input type="date" disabled={!isDraft} {...register('orderDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('salesOrders.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('salesOrders.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('salesOrders.unitPrice'))}</th>
                    {!isNew && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('deliveredQuantity', t('salesOrders.deliveredQuantity'))}</th>}
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
                              options={[{ value: 0, label: t('salesOrders.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 100 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isDraft} style={{ width: 100 }} {...register(`lines.${index}.unitPrice` as const, { valueAsNumber: true })} />
                      </td>
                      {!isNew && (
                        <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>{order?.lines[index]?.deliveredQuantity ?? 0}</td>
                      )}
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
                {t('salesOrders.addLine')}
              </Button>
            )}

            {order && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('salesOrders.subtotal')}: {order.subtotal.toFixed(2)}</span>
              </div>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
