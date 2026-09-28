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
import {
  useAcceptSalesQuote, useCreateSalesQuote, useRejectSalesQuote, useSalesQuote, useSubmitSalesQuote, useUpdateSalesQuote
} from './api';
import type { SalesQuoteLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  customerId: number | '';
  quoteDate: string;
  validUntil: string;
  lines: SalesQuoteLineInput[];
}

const emptyLine = (): SalesQuoteLineInput => ({ itemId: 0, quantity: 0, unitPrice: 0 });

/** /sales/quotes/:id — screen #5 (04-Module-Sales.md, section 5). Draft → Sent → Accepted/Rejected
 * (section 4.1) — editing locks after Draft; an Accepted quote is later picked up by
 * CreateSalesOrderCommand to convert into an order (قاعدة 15). */
export function SalesQuoteEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const quoteId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: quote, isLoading } = useSalesQuote(quoteId);
  const { data: customers } = useCustomersList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('SALES_QUOTE');

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { customerId: '', quoteDate: todayLocal(), validUntil: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  useEffect(() => {
    if (quote) {
      reset({
        customerId: quote.customerId,
        quoteDate: quote.quoteDate,
        validUntil: quote.validUntil,
        lines: quote.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitPrice: l.unitPrice, discountAmount: l.discountAmount }))
      });
    }
  }, [quote, reset]);

  const createMutation = useCreateSalesQuote();
  const updateMutation = useUpdateSalesQuote(quoteId ?? 0);
  const submitMutation = useSubmitSalesQuote(quoteId ?? 0);
  const acceptMutation = useAcceptSalesQuote(quoteId ?? 0);
  const rejectMutation = useRejectSalesQuote(quoteId ?? 0);

  const isDraft = isNew || quote?.status === 'Draft';
  const isSent = quote?.status === 'Sent';

  const customerOptions = (customers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));
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
    const lines = values.lines.map((l) => ({
      itemId: Number(l.itemId), quantity: Number(l.quantity), unitPrice: Number(l.unitPrice),
      discountAmount: l.discountAmount ? Number(l.discountAmount) : undefined
    }));
    const common = {
      customerId: Number(values.customerId),
      quoteDate: values.quoteDate,
      validUntil: values.validUntil,
      lines
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('salesQuotes.createSuccess'), 'success');
        navigate(`/sales/quotes/${newId}`);
      } else if (quote) {
        await updateMutation.mutateAsync({ ...common, rowVersion: quote.rowVersion });
        showToast(t('salesQuotes.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('salesQuotes.addQuote') : `${t('salesQuotes.title')} — ${quote?.quoteNumber ?? ''}`}</h2>
        {quote && <StatusBadge status={quote.status} />}
      </div>

      <ActionBar
        primary={isDraft ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/sales/quotes') },
          ...(isDraft && !isNew ? [{ key: 'submit', label: t('salesQuotes.submit'), onClick: () => runAction(() => submitMutation.mutateAsync(), t('salesQuotes.submitSuccess')) }] : []),
          ...(isSent ? [{ key: 'accept', label: t('salesQuotes.accept'), onClick: () => runAction(() => acceptMutation.mutateAsync(), t('salesQuotes.acceptSuccess')) }] : [])
        ]}
        destructive={
          isSent
            ? [{ key: 'reject', label: t('salesQuotes.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('salesQuotes.rejectSuccess')), confirmMessage: t('salesQuotes.rejectConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('quoteNumber', t('salesQuotes.quoteNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (quote?.quoteNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('customer', t('salesQuotes.customer'))}>
                <Controller
                  control={control}
                  name="customerId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isDraft} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={customerOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('quoteDate', t('salesQuotes.quoteDate'))}>
                <Input type="date" disabled={!isDraft} {...register('quoteDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('validUntil', t('salesQuotes.validUntil'))}>
                <Input type="date" disabled={!isDraft} {...register('validUntil')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('salesQuotes.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('salesQuotes.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('salesQuotes.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineDiscount', t('salesQuotes.lineDiscount'))}</th>
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
                              options={[{ value: 0, label: t('salesQuotes.selectItem') }, ...itemOptions]}
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
                        <Input type="number" step="0.01" disabled={!isDraft} style={{ width: 100 }} {...register(`lines.${index}.discountAmount` as const, { valueAsNumber: true })} />
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
                {t('salesQuotes.addLine')}
              </Button>
            )}

            {quote && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('salesQuotes.subtotal')}: {quote.subtotal.toFixed(2)}</span>
              </div>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
