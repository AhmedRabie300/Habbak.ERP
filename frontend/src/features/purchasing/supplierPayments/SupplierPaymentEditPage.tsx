import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
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
import { useSuppliersList } from '../suppliers/api';
import { useAccountsList } from '../../accounting/accounts/api';
import { useApplyDefaultCurrency, useCurrenciesList } from '../../organization/currencies/api';
import {
  useCancelSupplierPayment, useCreateSupplierPayment, usePayableInvoicesList, usePostSupplierPayment,
  useReverseSupplierPayment, useSupplierPayment, useUpdateSupplierPayment
} from './api';
import { todayLocal } from '../../../lib/date';
import { InvoiceAllocationTable } from './InvoiceAllocationTable';

interface FormValues {
  supplierId: number | '';
  purchaseInvoiceId: number | '';
  voucherDate: string;
  treasuryAccountId: number | '';
  description: string;
  amount: number;
  currencyCode: string | '';
  exchangeRate: number;
}

/** /purchasing/supplier-payments/:id — screen #9. Draft → Posted (applies Amount to the linked
 * PurchaseInvoice's AmountPaid, PostVoucherCommand) → Reverse un-applies it. No dedicated entity:
 * this is the Voucher screen (VoucherType.Payment, CounterpartyType.Supplier) under Purchasing's
 * own vocabulary (Supplier/Invoice pickers instead of generic counterparty fields). */
export function SupplierPaymentEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const paymentId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: payment, isLoading } = useSupplierPayment(paymentId);
  const { data: suppliers } = useSuppliersList();
  const { data: accounts } = useAccountsList(true);
  const { data: currencies } = useCurrenciesList();
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_PAYMENTS');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('ACCOUNTING_PAYMENT_VOUCHERS', 'SupplierPayment', paymentId);

  const { register, control, handleSubmit, reset, watch, setValue } = useForm<FormValues>({
    defaultValues: {
      supplierId: '', purchaseInvoiceId: '', voucherDate: todayLocal(),
      treasuryAccountId: '', description: '', amount: 0, currencyCode: '', exchangeRate: 1
    }
  });
  // Remarks3 item 1: a new record starts in the default currency.
  useApplyDefaultCurrency(isNew, watch('currencyCode'), (code) => setValue('currencyCode', code), 'code');

  const supplierId = watch('supplierId');
  const { data: payableInvoices } = usePayableInvoicesList(supplierId === '' ? undefined : Number(supplierId));

  useEffect(() => {
    if (payment) {
      reset({
        supplierId: payment.counterpartyId ?? '',
        purchaseInvoiceId: payment.relatedInvoiceId ?? '',
        voucherDate: payment.voucherDate,
        treasuryAccountId: payment.treasuryAccountId,
        description: payment.description ?? '',
        amount: payment.amount,
        currencyCode: payment.currencyCode,
        exchangeRate: payment.exchangeRate
      });
    }
  }, [payment, reset]);

  const createMutation = useCreateSupplierPayment();
  const updateMutation = useUpdateSupplierPayment(paymentId ?? 0);
  const postMutation = usePostSupplierPayment(paymentId ?? 0);
  const cancelMutation = useCancelSupplierPayment(paymentId ?? 0);
  const reverseMutation = useReverseSupplierPayment(paymentId ?? 0);

  const isEditable = isNew || payment?.status === 'Draft';

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const invoiceOptions = (payableInvoices ?? []).map((i) => ({
    value: i.id, label: `${i.invoiceNumber} — متبقي ${i.remainingAmount.toFixed(2)} ${i.currencyCode}`
  }));
  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));
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
    if (!checkDescriptionMandatory(values.description)) return;
    const common = {
      voucherDate: values.voucherDate,
      treasuryAccountId: Number(values.treasuryAccountId),
      description: values.description || undefined,
      supplierId: Number(values.supplierId),
      purchaseInvoiceId: values.purchaseInvoiceId === '' ? undefined : Number(values.purchaseInvoiceId),
      amount: Number(values.amount),
      currencyCode: String(values.currencyCode),
      exchangeRate: Number(values.exchangeRate),
      baseCurrencyAmount: Number(values.amount) * Number(values.exchangeRate)
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('supplierPayments.createSuccess'), 'success');
        navigate(`/purchasing/supplier-payments/${newId}`);
      } else if (payment) {
        await updateMutation.mutateAsync({ ...common, rowVersion: payment.rowVersion });
        showToast(t('supplierPayments.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handlePost = async () => {
    if (!checkAttachmentMandatory()) return;
    try {
      const result = await postMutation.mutateAsync();
      showToast(result.status === 'Posted' ? t('supplierPayments.postSuccessPosted') : t('supplierPayments.postSuccessPending'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('supplierPayments.addSupplierPayment') : `${t('supplierPayments.title')} — ${payment?.voucherNumber ?? ''}`}</h2>
        {payment && <StatusBadge status={payment.status} />}
      </div>

      <ActionBar
        primary={
          payment?.status === 'Draft'
            ? { key: 'post', label: t('common.post'), onClick: handlePost }
            : isEditable
              ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
              : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/supplier-payments') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(payment?.status === 'Draft' ? [{ key: 'saveChanges', label: t('common.saveChanges'), onClick: () => onSave() }] : []),
          ...(payment?.status === 'Posted' ? [{ key: 'reverse', label: t('common.reverse'), onClick: () => runAction(() => reverseMutation.mutateAsync(), t('supplierPayments.reverseSuccess')) }] : [])
        ]}
        destructive={
          payment?.status === 'Draft'
            ? [{ key: 'cancel', label: t('supplierPayments.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('supplierPayments.cancelSuccess')), confirmMessage: t('supplierPayments.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('voucherNumber', t('supplierPayments.voucherNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (payment?.voucherNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('supplier', t('supplierPayments.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('purchaseInvoice', t('supplierPayments.purchaseInvoice'))}>
                <Controller
                  control={control}
                  name="purchaseInvoiceId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={invoiceOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('voucherDate', t('supplierPayments.voucherDate'))}>
                <Input type="date" disabled={!isEditable} {...register('voucherDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('treasuryAccount', t('supplierPayments.treasuryAccount'))}>
                <Controller
                  control={control}
                  name="treasuryAccountId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={accountOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('currency', t('supplierPayments.currency'))}>
                <Controller
                  control={control}
                  name="currencyCode"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 140 }} value={field.value} onChange={(v) => field.onChange(v)} options={currencyOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('exchangeRate', t('supplierPayments.exchangeRate'))}>
                <Input type="number" step="0.000001" disabled={!isEditable} style={{ width: 120 }} {...register('exchangeRate', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('amount', t('supplierPayments.amount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 140 }} {...register('amount', { valueAsNumber: true })} />
              </FieldWrapper>
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('description', t('supplierPayments.description'))}>
                <textarea
                  disabled={!isEditable}
                  rows={3}
                  style={{
                    width: '100%', padding: '9px 12px', borderRadius: 'var(--radius-chip)', border: '1px solid transparent',
                    background: 'var(--color-surface-2)', fontSize: 14, fontFamily: 'inherit', resize: 'vertical', boxSizing: 'border-box'
                  }}
                  {...register('description')}
                />
              </FieldWrapper>
            </div>
          </CardBody>
        </Card>
      </form>

      <InvoiceAllocationTable
        paymentId={paymentId}
        supplierId={supplierId === '' ? undefined : Number(supplierId)}
        amount={Number(watch('amount')) || 0}
        currencyCode={String(watch('currencyCode') || '')}
        editable={isEditable}
      />

      {showAttachments && !isNew && <AttachmentPanel entityType="SupplierPayment" entityId={paymentId} />}
    </div>
  );
}
