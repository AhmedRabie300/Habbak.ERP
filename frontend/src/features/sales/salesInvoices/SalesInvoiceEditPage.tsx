import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { JournalEntryLinks } from '../../accounting/journalEntries/JournalEntryLinks';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getApiError, getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCustomersList } from '../customers/api';
import { useItemsList } from '../../inventory/items/api';
import { useConfirmedSalesOrdersList, useSalesOrder } from '../salesOrders/api';
import {
  useCancelSalesInvoice, useCreateSalesInvoice, usePostSalesInvoice, useRejectSalesInvoice, useSalesInvoice, useUpdateSalesInvoice
} from './api';
import type { SalesInvoiceLineInput } from './types';
import { todayLocal } from '../../../lib/date';
import { useFieldAccess } from '../../auth/access';

const PAYMENT_TYPES = ['Cash', 'Credit'] as const;

interface FormValues {
  customerId: number | '';
  invoiceDate: string;
  sourceOrderId: number | '';
  paymentType: (typeof PAYMENT_TYPES)[number];
  taxAmount: number;
  discountAmount: string;
  lines: SalesInvoiceLineInput[];
}

const emptyLine = (): SalesInvoiceLineInput => ({ itemId: 0, quantity: 0, unitPrice: 0 });

/** /sales/invoices/:id — screen #7 (04-Module-Sales.md, section 5), شاشة تفاعلية: بانر تحذير فوري
 * عند تجاوز حد الائتمان (قاعدة 1) + زر "تجاوز بصلاحية خاصة" يعيد الحفظ مع
 * CreditLimitOverrideApproved = true. Picking a confirmed order copies its lines automatically,
 * same pattern as SalesOrderEditPage's own quote picker. */
export function SalesInvoiceEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const invoiceId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [creditLimitWarning, setCreditLimitWarning] = useState<string | null>(null);
  // The invoice's and the lines' discount share one rule (the server masks both).
  const discountAccess = useFieldAccess('SALES_INVOICES', 'SalesInvoice')('DiscountAmount');

  const { data: invoice, isLoading } = useSalesInvoice(invoiceId);
  const { data: customers } = useCustomersList();
  const { data: items } = useItemsList();
  const { data: confirmedOrders } = useConfirmedSalesOrdersList();
  const { label } = useFieldLabels('SALES_INVOICE');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: { customerId: '', invoiceDate: todayLocal(), sourceOrderId: '', paymentType: 'Cash', taxAmount: 0, discountAmount: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const selectedOrderId = watch('sourceOrderId');
  const { data: selectedOrder } = useSalesOrder(isNew && selectedOrderId ? Number(selectedOrderId) : undefined);

  useEffect(() => {
    if (selectedOrder && isNew) {
      setValue('customerId', selectedOrder.customerId);
      setValue('lines', selectedOrder.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice })));
    }
  }, [selectedOrder, isNew, setValue]);

  useEffect(() => {
    if (invoice) {
      reset({
        customerId: invoice.customerId,
        invoiceDate: invoice.invoiceDate,
        sourceOrderId: invoice.sourceOrderId ?? '',
        paymentType: invoice.paymentType as (typeof PAYMENT_TYPES)[number],
        taxAmount: invoice.taxAmount,
        discountAmount: invoice.discountAmount?.toString() ?? '',
        lines: invoice.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discountAmount }))
      });
    }
  }, [invoice, reset]);

  const createMutation = useCreateSalesInvoice();
  const updateMutation = useUpdateSalesInvoice(invoiceId ?? 0);
  const postMutation = usePostSalesInvoice(invoiceId ?? 0);
  const rejectMutation = useRejectSalesInvoice(invoiceId ?? 0);
  const cancelMutation = useCancelSalesInvoice(invoiceId ?? 0);

  const isDraft = isNew || invoice?.status === 'Draft';

  const customerOptions = (customers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const orderOptions = (confirmedOrders ?? []).map((o) => ({ value: o.id, label: `${o.orderNumber} — ${o.customerNameAr}` }));

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    try {
      await action();
      showToast(successMessage, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const save = async (values: FormValues, creditLimitOverrideApproved: boolean) => {
    const lines = values.lines.map((l) => ({
      itemId: Number(l.itemId), quantity: Number(l.quantity), unitPrice: Number(l.unitPrice),
      discountAmount: l.discountAmount ? Number(l.discountAmount) : undefined
    }));
    const common = {
      customerId: Number(values.customerId),
      invoiceDate: values.invoiceDate,
      paymentType: values.paymentType,
      creditLimitOverrideApproved,
      taxAmount: Number(values.taxAmount),
      discountAmount: values.discountAmount ? Number(values.discountAmount) : undefined,
      lines
    };

    try {
      setCreditLimitWarning(null);
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          ...common,
          sourceOrderId: values.sourceOrderId === '' ? undefined : Number(values.sourceOrderId)
        });
        showToast(t('salesInvoices.createSuccess'), 'success');
        navigate(`/sales/invoices/${newId}`);
      } else if (invoice) {
        await updateMutation.mutateAsync({ ...common, rowVersion: invoice.rowVersion });
        showToast(t('salesInvoices.updateSuccess'), 'success');
      }
    } catch (error) {
      const apiError = getApiError(error);
      if (apiError?.errorCode === 'SALES-INVOICE-CREDIT-LIMIT-EXCEEDED') {
        setCreditLimitWarning(apiError.message);
      } else {
        const message = getFieldErrorMessage(error);
        if (message) showToast(message, 'error');
      }
    }
  };

  const onSave = handleSubmit((values) => save(values, false));
  const onOverrideSave = handleSubmit((values) => save(values, true));

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('salesInvoices.addInvoice') : `${t('salesInvoices.title')} — ${invoice?.invoiceNumber ?? ''}`}</h2>
        {invoice && <StatusBadge status={invoice.status} />}
      </div>

      <ActionBar
        primary={isDraft ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/sales/invoices') },
          ...(isDraft && !isNew ? [{ key: 'post', buttonCode: 'Post', label: t('salesInvoices.post'), onClick: () => runAction(() => postMutation.mutateAsync(), t('salesInvoices.postSuccess')) }] : [])
        ]}
        destructive={[
          ...(isDraft && !isNew ? [{ key: 'reject', buttonCode: 'Reject', label: t('salesInvoices.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('salesInvoices.rejectSuccess')), confirmMessage: t('salesInvoices.rejectConfirm') }] : []),
          // A posted invoice can be cancelled too: the server posts the reversing entry, and refuses
          // once money was received, goods delivered or a return exists.
          ...((isDraft && !isNew) || invoice?.status === 'Posted'
            ? [{
                key: 'cancel', buttonCode: 'Cancel',
                label: t('salesInvoices.cancel'),
                onClick: () => runAction(() => cancelMutation.mutateAsync(), t('salesInvoices.cancelSuccess')),
                confirmMessage: t(invoice?.journalEntryId ? 'salesInvoices.cancelPostedConfirm' : 'salesInvoices.cancelConfirm')
              }]
            : [])
        ]}
      />

      {creditLimitWarning && (
        <div style={{ padding: '10px 14px', background: 'var(--warning-bg, #fff3cd)', border: '1px solid var(--warning-border, #f0c36d)', borderRadius: 6, fontSize: 13, display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
          <span>⚠️ {creditLimitWarning}</span>
          <Button type="button" variant="secondary" onClick={() => onOverrideSave()}>{t('salesInvoices.overrideCreditLimit')}</Button>
        </div>
      )}

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('invoiceNumber', t('salesInvoices.invoiceNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (invoice?.invoiceNumber ?? '')} disabled />
              </FieldWrapper>
              {isNew && (
                <FieldWrapper label={label('sourceOrder', t('salesInvoices.sourceOrder'))}>
                  <Controller
                    control={control}
                    name="sourceOrderId"
                    render={({ field }) => (
                      <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={orderOptions} />
                    )}
                  />
                </FieldWrapper>
              )}
              {!isNew && invoice?.sourceOrderNumber && (
                <FieldWrapper label={label('sourceOrder', t('salesInvoices.sourceOrder'))}>
                  <Input value={invoice.sourceOrderNumber} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('customer', t('salesInvoices.customer'))}>
                <Controller
                  control={control}
                  name="customerId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={customerOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('invoiceDate', t('salesInvoices.invoiceDate'))}>
                <Input type="date" disabled={!isDraft} {...register('invoiceDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('paymentType', t('salesInvoices.paymentType'))}>
                <Controller
                  control={control}
                  name="paymentType"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isDraft}
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={PAYMENT_TYPES.map((pt) => ({ value: pt, label: t(`salesInvoices.type${pt}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('taxAmount', t('salesInvoices.taxAmount'))}>
                <Input type="number" step="0.01" disabled={!isDraft} style={{ width: 120 }} {...register('taxAmount', { valueAsNumber: true })} />
              </FieldWrapper>
              {discountAccess.canView && (
                <FieldWrapper label={label('discountAmount', t('salesInvoices.discountAmount'))}>
                  <Input type="number" step="0.01" disabled={!isDraft || !discountAccess.canEdit} style={{ width: 120 }} {...register('discountAmount')} />
                </FieldWrapper>
              )}
              {!isNew && invoice?.creditLimitOverrideApproved && (
                <FieldWrapper label={label('creditLimitOverrideApproved', t('salesInvoices.creditLimitOverrideApproved'))}>
                  <Input value={t('common.yes')} disabled style={{ width: 80 }} />
                </FieldWrapper>
              )}
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('salesInvoices.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('salesInvoices.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('salesInvoices.unitPrice'))}</th>
                    {discountAccess.canView && <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineDiscount', t('salesInvoices.lineDiscount'))}</th>}
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
                              options={[{ value: 0, label: t('salesInvoices.selectItem') }, ...itemOptions]}
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
                      {discountAccess.canView && (
                        <td style={{ padding: 8 }}>
                          <Input type="number" step="0.01" disabled={!isDraft || !discountAccess.canEdit} style={{ width: 100 }} {...register(`lines.${index}.discountAmount` as const, { valueAsNumber: true })} />
                        </td>
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
                {t('salesInvoices.addLine')}
              </Button>
            )}

            {invoice && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('salesInvoices.subtotal')}: {invoice.subtotal.toFixed(2)}</span>
                <span>{t('salesInvoices.totalAmount')}: {invoice.totalAmount.toFixed(2)}</span>
                <span>{t('salesInvoices.amountPaid')}: {invoice.amountPaid.toFixed(2)}</span>
                <JournalEntryLinks {...invoice} />
              </div>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
