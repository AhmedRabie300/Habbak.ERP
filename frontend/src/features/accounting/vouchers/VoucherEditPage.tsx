import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { AttachmentPanel } from '../../../ui-kit/AttachmentPanel';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Alert } from '../../../ui-kit/Alert';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useMandatorySettings } from '../../settings/codingRules/useMandatorySettings';
import { getFieldErrorMessage } from '../../../app/api';
import { BranchField } from '../../organization/branches/BranchField';
import { useAccountsList } from '../accounts/api';
import { useSuppliersList } from '../../purchasing/suppliers/api';
import { useCreateVoucher, usePostVoucher, useCancelVoucher, useReverseVoucher, useUpdateVoucher, useVoucher } from './api';
import type { VoucherFormValues, VoucherKind } from './types';
import { todayLocal } from '../../../lib/date';

/** Shared Edit screen for /accounting/receipt-vouchers/:id and /accounting/payment-vouchers/:id. */
export function VoucherEditPage({ kind }: { kind: VoucherKind }) {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const voucherId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const title = kind === 'receipt-vouchers' ? t('vouchers.newReceiptTitle') : t('vouchers.newPaymentTitle');
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: voucher, isLoading } = useVoucher(kind, voucherId);
  const { data: accounts } = useAccountsList(true);
  const { data: suppliers } = useSuppliersList();
  const { label } = useFieldLabels('ACCOUNTING_VOUCHER_EDIT');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings(
    kind === 'receipt-vouchers' ? 'ACCOUNTING_RECEIPT_VOUCHERS' : 'ACCOUNTING_PAYMENT_VOUCHERS', 'Voucher', voucherId
  );

  const { register, control, handleSubmit, reset, watch } = useForm<VoucherFormValues>({
    defaultValues: {
      voucherDate: todayLocal(),
      treasuryAccountId: 0,
      counterpartyType: 'Other',
      amount: 0,
      currencyCode: 'EGP',
      exchangeRate: 1,
      baseCurrencyAmount: 0
    }
  });

  useEffect(() => {
    if (voucher) {
      reset({
        branchId: voucher.branchId,
        voucherDate: voucher.voucherDate,
        treasuryAccountId: voucher.treasuryAccountId,
        description: voucher.description,
        counterpartyType: voucher.counterpartyType as VoucherFormValues['counterpartyType'],
        counterpartyId: voucher.counterpartyId,
        directAccountId: voucher.directAccountId,
        amount: voucher.amount,
        currencyCode: voucher.currencyCode,
        exchangeRate: voucher.exchangeRate,
        baseCurrencyAmount: voucher.baseCurrencyAmount
      });
    }
  }, [voucher, reset]);

  const createMutation = useCreateVoucher(kind);
  const updateMutation = useUpdateVoucher(kind, voucherId ?? 0);
  const postMutation = usePostVoucher(kind, voucherId ?? 0);
  const cancelMutation = useCancelVoucher(kind, voucherId ?? 0);
  const reverseMutation = useReverseVoucher(kind, voucherId ?? 0);

  const counterpartyType = watch('counterpartyType');
  const isEditable = isNew || voucher?.status === 'Draft';

  const onSubmit = handleSubmit(async (values) => {
    if (!checkDescriptionMandatory(values.description)) return;
    if (!values.branchId) {
      showToast(t('vouchers.branchRequired'), 'error');
      return;
    }
    const payload: VoucherFormValues = {
      ...values,
      amount: Number(values.amount),
      exchangeRate: 1,
      baseCurrencyAmount: Number(values.amount),
      counterpartyId: values.counterpartyType === 'Other' ? undefined : Number(values.counterpartyId),
      directAccountId: values.counterpartyType === 'Other' ? Number(values.directAccountId) : undefined
    };

    try {
      if (isNew) {
        const newId = await createMutation.mutateAsync(payload);
        showToast(t('vouchers.createSuccess'), 'success');
        navigate(`/accounting/${kind}/${newId}`);
      } else if (voucher) {
        await updateMutation.mutateAsync({ ...payload, rowVersion: voucher.rowVersion });
        showToast(t('vouchers.updateSuccess'), 'success');
      }
    } catch (error) {
      // General errors are toasted centrally — only field-level validation errors
      // (excluded from that central toast) need surfacing here.
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handlePost = async () => {
    if (!checkAttachmentMandatory()) return;
    try {
      const result = await postMutation.mutateAsync();
      showToast(result.status === 'Posted' ? t('vouchers.postSuccessPosted') : t('vouchers.postSuccessPending'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCancel = async () => {
    try {
      await cancelMutation.mutateAsync();
      showToast(t('vouchers.cancelSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleReverse = async () => {
    try {
      await reverseMutation.mutateAsync();
      showToast(t('vouchers.reverseSuccess'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, maxWidth: 640 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{isNew ? title : `${title} ${voucher?.voucherNumber ?? ''}`}</h2>
        {voucher && <StatusBadge status={voucher.status} />}
      </div>

      <ActionBar
        primary={
          voucher?.status === 'Draft'
            ? { key: 'post', label: t('common.post'), onClick: handlePost }
            : isEditable
              ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSubmit() }
              : undefined
        }
        secondary={[
          { key: 'back', label: isEditable ? t('common.cancel') : t('common.back'), onClick: () => navigate(`/accounting/${kind}`) },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(voucher?.status === 'Draft'
            ? [{ key: 'saveChanges', label: t('common.saveChanges'), onClick: () => onSubmit() }]
            : []),
          ...(voucher?.status === 'Posted'
            ? [{ key: 'reverse', label: t('common.reverse'), onClick: handleReverse }]
            : [])
        ]}
        destructive={
          voucher?.status === 'Draft'
            ? [
                {
                  key: 'cancelVoucher',
                  label: t('vouchers.cancelVoucher'),
                  onClick: handleCancel,
                  confirmTitle: t('vouchers.cancelVoucher'),
                  confirmMessage: t('vouchers.cancelVoucher')
                }
              ]
            : []
        }
      />

      {voucher?.status === 'Posted' && <Alert tone="warning">{t('vouchers.lockedPosted')}</Alert>}

      <form onSubmit={onSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Card>
        <CardBody>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <FieldWrapper label={label('voucherNumber', t('vouchers.voucherNumber'))}>
            <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (voucher?.voucherNumber ?? '')} disabled />
          </FieldWrapper>

          <FieldWrapper label={label('date', t('vouchers.date'))}>
            <Input type="date" disabled={!isEditable} {...register('voucherDate')} />
          </FieldWrapper>

          <Controller
            control={control}
            name="branchId"
            render={({ field }) => (
              <BranchField
                value={field.value}
                onChange={field.onChange}
                label={label('branch', t('vouchers.branch'))}
                autoSelect={isNew}
                disabled={!isEditable}
              />
            )}
          />

          <FieldWrapper label={label('treasuryAccount', t('vouchers.treasuryAccount'))}>
            <Controller
              control={control}
              name="treasuryAccountId"
              render={({ field }) => (
                <SearchableSelect
                  disabled={!isEditable}
                  value={field.value}
                  onChange={(v) => field.onChange(Number(v))}
                  options={[
                    { value: 0, label: t('common.selectAccount') },
                    ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
                  ]}
                />
              )}
            />
          </FieldWrapper>

          <FieldWrapper label={label('counterpartyType', t('vouchers.counterpartyType'))}>
            <Controller
              control={control}
              name="counterpartyType"
              render={({ field }) => (
                <SearchableSelect
                  disabled={!isEditable}
                  value={field.value}
                  onChange={(v) => field.onChange(v as VoucherFormValues['counterpartyType'])}
                  options={[
                    { value: 'Other', label: t('vouchers.other') },
                    { value: 'Customer', label: t('vouchers.customer') },
                    { value: 'Supplier', label: t('vouchers.supplier') },
                    { value: 'Employee', label: t('vouchers.employee') }
                  ]}
                />
              )}
            />
          </FieldWrapper>

          {counterpartyType === 'Other' ? (
            <FieldWrapper label={label('directAccount', t('vouchers.directAccount'))}>
              <Controller
                control={control}
                name="directAccountId"
                render={({ field }) => (
                  <SearchableSelect
                    disabled={!isEditable}
                    value={field.value}
                    onChange={(v) => field.onChange(Number(v))}
                    options={[
                      { value: 0, label: t('common.selectAccount') },
                      ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
                    ]}
                  />
                )}
              />
            </FieldWrapper>
          ) : counterpartyType === 'Supplier' ? (
            <FieldWrapper label={label('counterpartyId', t('vouchers.counterpartyId'))}>
              <Controller
                control={control}
                name="counterpartyId"
                render={({ field }) => (
                  <SearchableSelect
                    disabled={!isEditable}
                    value={field.value ?? ''}
                    onChange={(v) => field.onChange(v === '' ? undefined : Number(v))}
                    options={(suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }))}
                  />
                )}
              />
            </FieldWrapper>
          ) : (
            <FieldWrapper label={label('counterpartyId', t('vouchers.counterpartyId'))}>
              <Input
                type="number"
                disabled={!isEditable}
                {...register('counterpartyId', { valueAsNumber: true })}
                title={t('vouchers.counterpartyNotSupported')}
              />
            </FieldWrapper>
          )}

          <FieldWrapper label={label('amount', t('vouchers.amount'))}>
            <Input type="number" step="0.01" disabled={!isEditable} {...register('amount', { valueAsNumber: true })} />
          </FieldWrapper>
        </div>

        {counterpartyType !== 'Other' && counterpartyType !== 'Supplier' && (
          <p style={{ fontSize: 12, color: 'var(--color-text-muted)', marginTop: 8 }}>{t('vouchers.counterpartyNotSupported')}</p>
        )}

        <div style={{ marginTop: 16, width: '100%' }}>
          <FieldWrapper label={label('description', t('vouchers.description'))}>
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

      {showAttachments && !isNew && <AttachmentPanel entityType="Voucher" entityId={voucherId} />}
    </div>
  );
}
