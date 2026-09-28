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
import { useBranchesList } from '../../organization/branches/api';
import { useItemsList } from '../../inventory/items/api';
import { UnitSelect, defaultUnitId } from '../../inventory/items/UnitSelect';
import {
  useApprovePurchaseRequest, useCancelPurchaseRequest, useCreatePurchaseRequest, usePurchaseRequest,
  useRejectPurchaseRequest, useSubmitPurchaseRequest, useUpdatePurchaseRequest
} from './api';
import type { PurchaseRequestLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'] as const;

interface FormValues {
  branchId: number | '';
  requestDate: string;
  priority: (typeof PRIORITIES)[number];
  reason: string;
  notes: string;
  lines: PurchaseRequestLineInput[];
}

const emptyLine = (): PurchaseRequestLineInput => ({ itemId: 0, quantity: 0, unitId: undefined });

/** /purchasing/purchase-requests/:id — screen #2 (03-Module-Purchasing.md, section 8). Standard
 * Draft → PendingApproval → Approved lifecycle (section 6.1) — Converted (once PurchaseOrder
 * exists) isn't reachable from this page yet. */
export function PurchaseRequestEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const requestId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: request, isLoading } = usePurchaseRequest(requestId);
  const { data: branches } = useBranchesList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('PURCHASING_PURCHASE_REQUEST');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      branchId: '',
      requestDate: todayLocal(),
      priority: 'Normal',
      reason: '',
      notes: '',
      lines: [emptyLine()]
    }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  // Each line's unit is one of its item's units (Fixes-Batch-2026-09-19).
  const unitLines = watch('lines');
  const unitItems = new Map((items ?? []).map((i) => [i.id, i]));

  useEffect(() => {
    if (request) {
      reset({
        branchId: request.branchId ?? '',
        requestDate: request.requestDate,
        priority: request.priority as (typeof PRIORITIES)[number],
        reason: request.reason ?? '',
        notes: request.notes ?? '',
        lines: request.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitId: l.unitId, notes: l.notes }))
      });
    }
  }, [request, reset]);

  const createMutation = useCreatePurchaseRequest();
  const updateMutation = useUpdatePurchaseRequest(requestId ?? 0);
  const submitMutation = useSubmitPurchaseRequest(requestId ?? 0);
  const approveMutation = useApprovePurchaseRequest(requestId ?? 0);
  const rejectMutation = useRejectPurchaseRequest(requestId ?? 0);
  const cancelMutation = useCancelPurchaseRequest(requestId ?? 0);

  const isDraft = isNew || request?.status === 'Draft';
  const isPendingApproval = request?.status === 'PendingApproval';
  const isCancellable = request?.status === 'Draft' || request?.status === 'PendingApproval' || request?.status === 'Approved';

  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }));
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
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), quantity: Number(l.quantity), unitId: l.unitId ? Number(l.unitId) : undefined, notes: l.notes || undefined }));
    const common = { requestDate: values.requestDate, priority: values.priority, reason: values.reason || undefined, notes: values.notes || undefined, lines };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({ branchId: Number(values.branchId), ...common });
        showToast(t('purchaseRequests.createSuccess'), 'success');
        navigate(`/purchasing/purchase-requests/${newId}`);
      } else if (request) {
        await updateMutation.mutateAsync({ ...common, rowVersion: request.rowVersion });
        showToast(t('purchaseRequests.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('purchaseRequests.addPurchaseRequest') : `${t('purchaseRequests.title')} — ${request?.requestNumber ?? ''}`}</h2>
        {request && <StatusBadge status={request.status} />}
      </div>

      <ActionBar
        primary={
          isDraft
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : isPendingApproval
              ? { key: 'approve', label: t('purchaseRequests.approve'), onClick: () => runAction(() => approveMutation.mutateAsync(), t('purchaseRequests.approveSuccess')) }
              : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/purchase-requests') },
          ...(!isNew && request?.status === 'Draft' ? [{ key: 'submit', label: t('purchaseRequests.submitForApproval'), onClick: () => runAction(() => submitMutation.mutateAsync(), t('purchaseRequests.submitSuccess')) }] : [])
        ]}
        destructive={[
          ...(isPendingApproval ? [{ key: 'reject', label: t('purchaseRequests.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('purchaseRequests.rejectSuccess')), confirmMessage: t('purchaseRequests.rejectConfirm') }] : []),
          ...(isCancellable ? [{ key: 'cancel', label: t('purchaseRequests.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('purchaseRequests.cancelSuccess')), confirmMessage: t('purchaseRequests.cancelConfirm') }] : [])
        ]}
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('requestNumber', t('purchaseRequests.requestNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (request?.requestNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('branch', t('purchaseRequests.branch'))}>
                <Controller
                  control={control}
                  name="branchId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isNew} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={branchOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('requestDate', t('purchaseRequests.requestDate'))}>
                <Input type="date" disabled={!isDraft} {...register('requestDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('priority', t('purchaseRequests.priority'))}>
                <Controller
                  control={control}
                  name="priority"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isDraft}
                      style={{ minWidth: 140 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v)}
                      options={PRIORITIES.map((p) => ({ value: p, label: t(`purchaseRequests.priority${p}`) }))}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('reason', t('purchaseRequests.reason'))}>
                <Input disabled={!isDraft} style={{ minWidth: 220 }} {...register('reason')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('purchaseRequests.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('purchaseRequests.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('purchaseRequests.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('lineNotes', t('purchaseRequests.lineNotes'))}</th>
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
                              onChange={(v) => {
                                field.onChange(Number(v));
                                setValue(`lines.${index}.unitId`, defaultUnitId(unitItems.get(Number(v))));
                              }}
                              options={[{ value: 0, label: t('purchaseRequests.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 110 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field }) => (
                            <UnitSelect
                              item={unitItems.get(Number(unitLines[index]?.itemId))}
                              value={field.value || defaultUnitId(unitItems.get(Number(unitLines[index]?.itemId)))}
                              onChange={field.onChange}
                              disabled={!isDraft}
                              width={140}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isDraft} style={{ width: 180 }} {...register(`lines.${index}.notes` as const)} />
                      </td>
                      {isDraft && (
                        <td>
                          <Button type="button" variant="ghost" onClick={() => remove(index)}>
                            {t('common.remove')}
                          </Button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {isDraft && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('purchaseRequests.addLine')}
              </Button>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('purchaseRequests.notes'))}>
                <textarea
                  disabled={!isDraft}
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
