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
import { useAccountsList } from '../accounts/api';
import {
  useAccountOpeningBalanceBatch, useCancelAccountOpeningBalanceBatch, useCreateAccountOpeningBalanceBatch,
  usePostAccountOpeningBalanceBatch, useUpdateAccountOpeningBalanceBatch
} from './api';
import type { AccountOpeningBalanceLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  transactionDate: string;
  notes: string;
  lines: AccountOpeningBalanceLineInput[];
}

const emptyLine = (): AccountOpeningBalanceLineInput => ({ accountId: 0, amount: 0 });

/** /accounting/opening-balances/:id — My Remarks/Remarks2.md, bugs 1.4/3.9. Draft → Posted: each
 * line's Debit/Credit side is resolved from the chosen account's own Nature (shown read-only,
 * never chosen by the user) — the live totals below the grid mirror IPostingService's own
 * balance check, which is what actually blocks Post server-side if they don't match. */
export function AccountOpeningBalanceBatchEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const batchId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: batch, isLoading } = useAccountOpeningBalanceBatch(batchId);
  const { data: accounts } = useAccountsList(true);
  const { label } = useFieldLabels('ACCOUNTING_OPENING_BALANCES');

  const { register, control, handleSubmit, reset, watch } = useForm<FormValues>({
    defaultValues: { transactionDate: todayLocal(), notes: '', lines: [emptyLine()] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  const watchedLines = watch('lines');

  useEffect(() => {
    if (batch) {
      reset({
        transactionDate: batch.transactionDate,
        notes: batch.notes ?? '',
        lines: batch.lines.map((l) => ({ accountId: l.accountId, amount: l.amount, notes: l.notes, accountNature: l.accountNature }))
      });
    }
  }, [batch, reset]);

  const createMutation = useCreateAccountOpeningBalanceBatch();
  const updateMutation = useUpdateAccountOpeningBalanceBatch(batchId ?? 0);
  const postMutation = usePostAccountOpeningBalanceBatch(batchId ?? 0);
  const cancelMutation = useCancelAccountOpeningBalanceBatch(batchId ?? 0);

  const isEditable = isNew || batch?.status === 'Draft';
  const canPost = !isNew && batch?.status === 'Draft';
  const canCancel = !isNew && batch?.status === 'Draft';

  const accountOptions = (accounts ?? []).map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` }));
  const accountsById = new Map((accounts ?? []).map((a) => [a.id, a]));

  const natureOf = (accountId: number, fallback?: string) => accountsById.get(accountId)?.nature ?? fallback ?? '';
  const totalDebit = watchedLines.reduce((sum, l) => sum + (natureOf(Number(l.accountId), l.accountNature) === 'Debit' ? Number(l.amount) || 0 : 0), 0);
  const totalCredit = watchedLines.reduce((sum, l) => sum + (natureOf(Number(l.accountId), l.accountNature) === 'Credit' ? Number(l.amount) || 0 : 0), 0);
  const isBalanced = Math.abs(totalDebit - totalCredit) < 0.001 && totalDebit > 0;

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
    const common = {
      transactionDate: values.transactionDate,
      notes: values.notes || undefined,
      lines: values.lines.map((l) => ({ accountId: Number(l.accountId), amount: Number(l.amount), notes: l.notes || undefined }))
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('accountOpeningBalances.createSuccess'), 'success');
        navigate(`/accounting/opening-balances/${newId}`);
      } else if (batch) {
        await updateMutation.mutateAsync({ ...common, rowVersion: batch.rowVersion });
        showToast(t('accountOpeningBalances.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('accountOpeningBalances.addBatch') : `${t('accountOpeningBalances.title')} — ${batch?.batchNumber ?? ''}`}</h2>
        {batch && <StatusBadge status={batch.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/accounting/opening-balances') },
          ...(canPost ? [{ key: 'post', label: t('common.post'), onClick: () => runAction(() => postMutation.mutateAsync(), t('accountOpeningBalances.postSuccess')) }] : [])
        ]}
        destructive={
          canCancel
            ? [{ key: 'cancel', label: t('accountOpeningBalances.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('accountOpeningBalances.cancelSuccess')), confirmMessage: t('accountOpeningBalances.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('batchNumber', t('accountOpeningBalances.batchNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (batch?.batchNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('transactionDate', t('accountOpeningBalances.transactionDate'))}>
                <Input type="date" disabled={!isEditable} {...register('transactionDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('account', t('accountOpeningBalances.account'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('accountNature', t('accountOpeningBalances.accountNature'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('amount', t('accountOpeningBalances.amount'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineNotes', t('accountOpeningBalances.lineNotes'))}</th>
                    {isEditable && <th />}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => {
                    const currentAccountId = Number(watchedLines[index]?.accountId ?? 0);
                    const nature = natureOf(currentAccountId, watchedLines[index]?.accountNature);
                    return (
                      <tr key={field.id}>
                        <td style={{ padding: 8 }}>
                          <Controller
                            control={control}
                            name={`lines.${index}.accountId` as const}
                            render={({ field }) => (
                              <SearchableSelect
                                disabled={!isEditable}
                                style={{ width: 260 }}
                                value={field.value}
                                onChange={(v) => field.onChange(Number(v))}
                                options={[{ value: 0, label: t('accountOpeningBalances.selectAccount') }, ...accountOptions]}
                              />
                            )}
                          />
                        </td>
                        <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>
                          {nature === 'Debit' ? t('accountOpeningBalances.debit') : nature === 'Credit' ? t('accountOpeningBalances.credit') : '—'}
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 130 }} {...register(`lines.${index}.amount` as const, { valueAsNumber: true })} />
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input disabled={!isEditable} style={{ width: 200 }} {...register(`lines.${index}.notes` as const)} />
                        </td>
                        {isEditable && (
                          <td>
                            <Button type="button" variant="ghost" onClick={() => remove(index)}>{t('common.remove')}</Button>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {isEditable && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('accountOpeningBalances.addLine')}
              </Button>
            )}

            <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
              <span>{t('accountOpeningBalances.totalDebit')}: {totalDebit.toFixed(2)}</span>
              <span>{t('accountOpeningBalances.totalCredit')}: {totalCredit.toFixed(2)}</span>
              <span style={{ color: isBalanced ? 'var(--color-success)' : 'var(--color-error)' }}>
                {isBalanced ? t('accountOpeningBalances.balanced') : t('accountOpeningBalances.unbalanced')}
              </span>
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('accountOpeningBalances.notes'))}>
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
    </div>
  );
}
