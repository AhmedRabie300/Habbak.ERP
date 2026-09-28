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
import { getFieldErrorMessage } from '../../../app/api';
import { BranchField } from '../../organization/branches/BranchField';
import { useAccountsList } from '../accounts/api';
import { useCreateTreasuryTransfer, usePostTreasuryTransfer, useTreasuryTransfer, useUpdateTreasuryTransfer } from './api';
import type { TreasuryTransferFormValues } from './types';
import { todayLocal } from '../../../lib/date';

/** /accounting/treasury-transfers/:id — Edit screen (01-Module-Accounting.md, section 5, screen 7). */
export function TreasuryTransferEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const transferId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: transfer, isLoading } = useTreasuryTransfer(transferId);
  const { data: accounts } = useAccountsList(true);
  const { label } = useFieldLabels('ACCOUNTING_TREASURY_TRANSFERS');

  const { register, control, handleSubmit, reset } = useForm<TreasuryTransferFormValues>({
    defaultValues: {
      transferDate: todayLocal(),
      fromTreasuryAccountId: 0,
      toTreasuryAccountId: 0,
      amount: 0,
      notes: ''
    }
  });

  useEffect(() => {
    if (transfer) {
      reset({
        branchId: transfer.branchId,
        transferDate: transfer.transferDate,
        fromTreasuryAccountId: transfer.fromTreasuryAccountId,
        toTreasuryAccountId: transfer.toTreasuryAccountId,
        amount: transfer.amount,
        notes: transfer.notes ?? ''
      });
    }
  }, [transfer, reset]);

  const createMutation = useCreateTreasuryTransfer();
  const updateMutation = useUpdateTreasuryTransfer(transferId ?? 0);
  const postMutation = usePostTreasuryTransfer(transferId ?? 0);

  const isEditable = isNew || transfer?.status === 'Draft';

  const accountOptions = [
    { value: 0, label: t('common.selectAccount') },
    ...(accounts?.map((a) => ({ value: a.id, label: `${a.code} - ${a.nameAr}` })) ?? [])
  ];

  const onSubmit = handleSubmit(async (values) => {
    if (!values.branchId) {
      showToast(t('treasuryTransfers.branchRequired'), 'error');
      return;
    }
    const payload: TreasuryTransferFormValues = {
      ...values,
      fromTreasuryAccountId: Number(values.fromTreasuryAccountId),
      toTreasuryAccountId: Number(values.toTreasuryAccountId),
      amount: Number(values.amount),
      notes: values.notes || undefined
    };

    try {
      if (isNew) {
        const newId = await createMutation.mutateAsync(payload);
        showToast(t('treasuryTransfers.createSuccess'), 'success');
        navigate(`/accounting/treasury-transfers/${newId}`);
      } else if (transfer) {
        await updateMutation.mutateAsync({ ...payload, rowVersion: transfer.rowVersion });
        showToast(t('treasuryTransfers.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handlePost = async () => {
    try {
      const result = await postMutation.mutateAsync();
      showToast(result.status === 'Posted' ? t('treasuryTransfers.postSuccessPosted') : t('treasuryTransfers.postSuccessPending'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('treasuryTransfers.newTransfer') : `${t('treasuryTransfers.title')} #${transfer?.id ?? ''}`}</h2>
        {transfer && <StatusBadge status={transfer.status} />}
      </div>

      <ActionBar
        primary={
          transfer?.status === 'Draft'
            ? { key: 'post', label: t('common.post'), onClick: handlePost }
            : isEditable
              ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSubmit() }
              : undefined
        }
        secondary={[
          { key: 'back', label: isEditable ? t('common.cancel') : t('common.back'), onClick: () => navigate('/accounting/treasury-transfers') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(transfer?.status === 'Draft'
            ? [{ key: 'saveChanges', label: t('common.saveChanges'), onClick: () => onSubmit() }]
            : [])
        ]}
      />

      {transfer?.status === 'Posted' && <Alert tone="warning">{t('treasuryTransfers.lockedPosted')}</Alert>}

      <form onSubmit={onSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('date', t('treasuryTransfers.date'))}>
                <Input type="date" disabled={!isEditable} {...register('transferDate')} />
              </FieldWrapper>

              <Controller
                control={control}
                name="branchId"
                render={({ field }) => (
                  <BranchField
                    value={field.value}
                    onChange={field.onChange}
                    label={label('branch', t('treasuryTransfers.branch'))}
                    autoSelect={isNew}
                    disabled={!isEditable}
                  />
                )}
              />

              <FieldWrapper label={label('fromAccount', t('treasuryTransfers.fromAccount'))}>
                <Controller
                  control={control}
                  name="fromTreasuryAccountId"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      value={field.value}
                      onChange={(v) => field.onChange(Number(v))}
                      options={accountOptions}
                    />
                  )}
                />
              </FieldWrapper>

              <FieldWrapper label={label('toAccount', t('treasuryTransfers.toAccount'))}>
                <Controller
                  control={control}
                  name="toTreasuryAccountId"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      value={field.value}
                      onChange={(v) => field.onChange(Number(v))}
                      options={accountOptions}
                    />
                  )}
                />
              </FieldWrapper>

              <FieldWrapper label={label('amount', t('treasuryTransfers.amount'))}>
                <Input type="number" step="0.01" disabled={!isEditable} {...register('amount', { valueAsNumber: true })} />
              </FieldWrapper>
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('treasuryTransfers.notes'))}>
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

      {showAttachments && !isNew && <AttachmentPanel entityType="TreasuryTransfer" entityId={transferId} />}
    </div>
  );
}
