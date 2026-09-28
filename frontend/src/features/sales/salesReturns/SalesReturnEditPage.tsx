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
import { useWarehousesList } from '../../inventory/warehouses/api';
import { useItemsList } from '../../inventory/items/api';
import { usePostedSalesInvoicesList, useSalesInvoice } from '../salesInvoices/api';
import {
  useCancelSalesReturn, useCreateSalesReturn, usePostSalesReturn, useRejectSalesReturn, useSalesReturn, useUpdateSalesReturn
} from './api';
import type { SalesReturnLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  customerId: number | '';
  warehouseId: number | '';
  returnDate: string;
  sourceInvoiceId: number | '';
  reason: string;
  lines: SalesReturnLineInput[];
}

const emptyLine = (): SalesReturnLineInput => ({ itemId: 0, quantity: 0, unitPrice: 0 });

/** /sales/returns/:id — screen #9 (04-Module-Sales.md, section 5). Picking a posted invoice
 * auto-fills the customer and lines. Posting increases StockBalance (قاعدة 19) — no reversal of
 * the source invoice's AmountPaid/Total, matching PurchaseReturn's own precedent on the other side
 * of the cycle. */
export function SalesReturnEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const returnId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: salesReturn, isLoading } = useSalesReturn(returnId);
  const { data: customers } = useCustomersList();
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();
  const { data: postedInvoices } = usePostedSalesInvoicesList();
  const { label } = useFieldLabels('SALES_RETURN');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: { customerId: '', warehouseId: '', returnDate: todayLocal(), sourceInvoiceId: '', reason: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const selectedInvoiceId = watch('sourceInvoiceId');
  const { data: selectedInvoice } = useSalesInvoice(isNew && selectedInvoiceId ? Number(selectedInvoiceId) : undefined);

  useEffect(() => {
    if (selectedInvoice && isNew) {
      setValue('customerId', selectedInvoice.customerId);
      setValue('lines', selectedInvoice.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice })));
    }
  }, [selectedInvoice, isNew, setValue]);

  useEffect(() => {
    if (salesReturn) {
      reset({
        customerId: salesReturn.customerId,
        warehouseId: salesReturn.warehouseId,
        returnDate: salesReturn.returnDate,
        sourceInvoiceId: salesReturn.sourceInvoiceId ?? '',
        reason: salesReturn.reason,
        lines: salesReturn.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, batchNumber: l.batchNumber }))
      });
    }
  }, [salesReturn, reset]);

  const createMutation = useCreateSalesReturn();
  const updateMutation = useUpdateSalesReturn(returnId ?? 0);
  const postMutation = usePostSalesReturn(returnId ?? 0);
  const rejectMutation = useRejectSalesReturn(returnId ?? 0);
  const cancelMutation = useCancelSalesReturn(returnId ?? 0);

  const isDraft = isNew || salesReturn?.status === 'Draft';

  const customerOptions = (customers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
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
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), quantity: Number(l.quantity), unitPrice: Number(l.unitPrice), batchNumber: l.batchNumber || undefined }));

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          customerId: Number(values.customerId),
          warehouseId: Number(values.warehouseId),
          returnDate: values.returnDate,
          sourceInvoiceId: values.sourceInvoiceId === '' ? undefined : Number(values.sourceInvoiceId),
          reason: values.reason,
          lines
        });
        showToast(t('salesReturns.createSuccess'), 'success');
        navigate(`/sales/returns/${newId}`);
      } else if (salesReturn) {
        await updateMutation.mutateAsync({
          customerId: Number(values.customerId),
          warehouseId: Number(values.warehouseId),
          returnDate: values.returnDate,
          reason: values.reason,
          lines,
          rowVersion: salesReturn.rowVersion
        });
        showToast(t('salesReturns.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('salesReturns.addReturn') : `${t('salesReturns.title')} — ${salesReturn?.returnNumber ?? ''}`}</h2>
        {salesReturn && <StatusBadge status={salesReturn.status} />}
      </div>

      <ActionBar
        primary={isDraft ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/sales/returns') },
          ...(isDraft && !isNew ? [{ key: 'post', label: t('salesReturns.post'), onClick: () => runAction(() => postMutation.mutateAsync(), t('salesReturns.postSuccess')) }] : [])
        ]}
        destructive={[
          ...(isDraft && !isNew ? [{ key: 'reject', label: t('salesReturns.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('salesReturns.rejectSuccess')), confirmMessage: t('salesReturns.rejectConfirm') }] : []),
          ...(isDraft && !isNew ? [{ key: 'cancel', label: t('salesReturns.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('salesReturns.cancelSuccess')), confirmMessage: t('salesReturns.cancelConfirm') }] : [])
        ]}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('returnNumber', t('salesReturns.returnNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (salesReturn?.returnNumber ?? '')} disabled />
              </FieldWrapper>
              {isNew && (
                <FieldWrapper label={label('sourceInvoice', t('salesReturns.sourceInvoice'))}>
                  <Controller
                    control={control}
                    name="sourceInvoiceId"
                    render={({ field }) => (
                      <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={invoiceOptions} />
                    )}
                  />
                </FieldWrapper>
              )}
              {!isNew && salesReturn?.sourceInvoiceNumber && (
                <FieldWrapper label={label('sourceInvoice', t('salesReturns.sourceInvoice'))}>
                  <Input value={salesReturn.sourceInvoiceNumber} disabled />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('customer', t('salesReturns.customer'))}>
                <Controller
                  control={control}
                  name="customerId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={customerOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('warehouse', t('salesReturns.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('returnDate', t('salesReturns.returnDate'))}>
                <Input type="date" disabled={!isDraft} {...register('returnDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('reason', t('salesReturns.reason'))}>
                <Input disabled={!isDraft} style={{ minWidth: 220 }} {...register('reason')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('salesReturns.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('salesReturns.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('salesReturns.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('salesReturns.batchNumber'))}</th>
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
                              options={[{ value: 0, label: t('salesReturns.selectItem') }, ...itemOptions]}
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
                {t('salesReturns.addLine')}
              </Button>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
