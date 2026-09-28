import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePurchaseInvoicesList } from '../purchaseInvoices/api';
import { useCreatePurchaseExpense, useDeletePurchaseExpense, usePurchaseExpense, useUpdatePurchaseExpense } from './api';

const EXPENSE_TYPES = ['Freight', 'Shipping', 'Customs', 'Loading', 'Insurance', 'Other'] as const;
const ALLOCATION_METHODS = ['ByValue', 'ByQuantity', 'ByWeight', 'Manual'] as const;

interface FormValues {
  purchaseInvoiceId: number | '';
  expenseType: (typeof EXPENSE_TYPES)[number];
  amount: number;
  allocationMethod: (typeof ALLOCATION_METHODS)[number];
  notes: string;
}

/** /purchasing/purchase-expenses/:id — screen #8. No workflow — always editable, same as
 * ContractItem/SupplierEvaluation; a breakdown row, not itself a posted transaction. */
export function PurchaseExpenseEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const expenseId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: expense, isLoading } = usePurchaseExpense(expenseId);
  const { data: invoicesPage } = usePurchaseInvoicesList({ page: 1, pageSize: 200 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_EXPENSES');

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { purchaseInvoiceId: '', expenseType: 'Freight', amount: 0, allocationMethod: 'ByValue', notes: '' }
  });

  useEffect(() => {
    if (expense) {
      reset({
        purchaseInvoiceId: expense.purchaseInvoiceId,
        expenseType: expense.expenseType as (typeof EXPENSE_TYPES)[number],
        amount: expense.amount,
        allocationMethod: expense.allocationMethod as (typeof ALLOCATION_METHODS)[number],
        notes: expense.notes ?? ''
      });
    }
  }, [expense, reset]);

  const createMutation = useCreatePurchaseExpense();
  const updateMutation = useUpdatePurchaseExpense(expenseId ?? 0);
  const deleteMutation = useDeletePurchaseExpense();

  const invoiceOptions = (invoicesPage?.items ?? []).map((i) => ({ value: i.id, label: i.invoiceNumber }));

  const onSave = handleSubmit(async (values) => {
    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          purchaseInvoiceId: Number(values.purchaseInvoiceId),
          expenseType: values.expenseType,
          amount: Number(values.amount),
          allocationMethod: values.allocationMethod,
          notes: values.notes || undefined
        });
        showToast(t('purchaseExpenses.createSuccess'), 'success');
        navigate(`/purchasing/purchase-expenses/${newId}`);
      } else if (expense) {
        await updateMutation.mutateAsync({
          expenseType: values.expenseType,
          amount: Number(values.amount),
          allocationMethod: values.allocationMethod,
          notes: values.notes || undefined,
          rowVersion: expense.rowVersion
        });
        showToast(t('purchaseExpenses.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!expenseId) return;
    try {
      await deleteMutation.mutateAsync(expenseId);
      showToast(t('purchaseExpenses.deleteSuccess'), 'success');
      navigate('/purchasing/purchase-expenses');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? t('purchaseExpenses.addPurchaseExpense') : `${t('purchaseExpenses.title')} — ${expense?.invoiceNumber ?? ''}`}</h2>
      </div>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/purchase-expenses') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('purchaseExpenses.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('purchaseInvoice', t('purchaseExpenses.purchaseInvoice'))}>
                <Controller
                  control={control}
                  name="purchaseInvoiceId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isNew} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={invoiceOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('expenseType', t('purchaseExpenses.expenseType'))}>
                <Controller
                  control={control}
                  name="expenseType"
                  render={({ field }) => (
                    <SearchableSelect
                      style={{ minWidth: 160 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={EXPENSE_TYPES.map((et) => ({ value: et, label: t(`purchaseExpenses.type${et}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('amount', t('purchaseExpenses.amount'))}>
                <Input type="number" step="0.01" style={{ width: 140 }} {...register('amount', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('allocationMethod', t('purchaseExpenses.allocationMethod'))}>
                <Controller
                  control={control}
                  name="allocationMethod"
                  render={({ field }) => (
                    <SearchableSelect
                      style={{ minWidth: 160 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={ALLOCATION_METHODS.map((m) => ({ value: m, label: t(`purchaseInvoices.allocation${m}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('purchaseExpenses.notes'))}>
                <textarea
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
    </div>
  );
}
