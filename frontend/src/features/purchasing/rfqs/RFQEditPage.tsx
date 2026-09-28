import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { AttachmentPanel } from '../../../ui-kit/AttachmentPanel';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { SearchableMultiSelect } from '../../../ui-kit/SearchableMultiSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useMandatorySettings } from '../../settings/codingRules/useMandatorySettings';
import { useSuppliersList } from '../suppliers/api';
import { useItemsList } from '../../inventory/items/api';
import { useUnitsOfMeasureList } from '../../inventory/unitsOfMeasure/api';
import {
  useAwardRFQ, useCancelRFQ, useCreateRFQ, useDeclineRFQSupplier, useRFQ, useSelectRFQSupplierQuote,
  useSendRFQ, useSetRFQSupplierQuote, useUpdateRFQ
} from './api';
import type { RFQLineInput, RFQSupplier } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  rfqDate: string;
  requiredDate: string;
  notes: string;
  lines: RFQLineInput[];
  supplierIds: number[];
}

const emptyLine = (): RFQLineInput => ({ itemId: 0, quantity: 0, unitId: 0 });

/** /purchasing/rfqs/:id — screen #3. Draft → Sent → UnderReview (auto, on first quote) →
 * Awarded. Lines/invited-suppliers lock after Draft; the quote-comparison grid below opens up
 * once Sent, letting the buyer record each supplier's price on their behalf and pick a winner per
 * line before awarding. */
export function RFQEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const rfqId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: rfq, isLoading } = useRFQ(rfqId);
  const { data: suppliers } = useSuppliersList();
  const { data: items } = useItemsList();
  const { data: units } = useUnitsOfMeasureList();
  const { label } = useFieldLabels('PURCHASING_RFQ');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_RFQ', 'RFQ', rfqId);

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { rfqDate: todayLocal(), requiredDate: '', notes: '', lines: [emptyLine()], supplierIds: [] }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  useEffect(() => {
    if (rfq) {
      reset({
        rfqDate: rfq.rfqDate,
        requiredDate: rfq.requiredDate ?? '',
        notes: rfq.notes ?? '',
        lines: rfq.lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, unitId: l.unitId })),
        supplierIds: rfq.suppliers.map((s) => s.supplierId)
      });
    }
  }, [rfq, reset]);

  const createMutation = useCreateRFQ();
  const updateMutation = useUpdateRFQ(rfqId ?? 0);
  const sendMutation = useSendRFQ(rfqId ?? 0);
  const awardMutation = useAwardRFQ(rfqId ?? 0);
  const cancelMutation = useCancelRFQ(rfqId ?? 0);
  const declineMutation = useDeclineRFQSupplier(rfqId ?? 0);
  const selectMutation = useSelectRFQSupplierQuote(rfqId ?? 0);

  const isEditable = isNew || rfq?.status === 'Draft';
  const canSend = !isNew && rfq?.status === 'Draft';
  const canAward = !isNew && rfq?.status === 'UnderReview';
  const canCancel = !isNew && (rfq?.status === 'Draft' || rfq?.status === 'Sent' || rfq?.status === 'UnderReview');
  const canQuote = !isNew && (rfq?.status === 'Sent' || rfq?.status === 'UnderReview');

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const unitOptions = (units ?? []).map((u) => ({ value: u.id, label: `${u.code} — ${u.nameAr}` }));

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
    if (!checkDescriptionMandatory(values.notes)) return;
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), quantity: Number(l.quantity), unitId: Number(l.unitId) }));
    const common = {
      rfqDate: values.rfqDate,
      requiredDate: values.requiredDate || undefined,
      notes: values.notes || undefined,
      lines,
      supplierIds: values.supplierIds.map(Number)
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('rfqs.createSuccess'), 'success');
        navigate(`/purchasing/rfqs/${newId}`);
      } else if (rfq) {
        await updateMutation.mutateAsync({ ...common, rowVersion: rfq.rowVersion });
        showToast(t('rfqs.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('rfqs.addRFQ') : `${t('rfqs.title')} — ${rfq?.rfqNumber ?? ''}`}</h2>
        {rfq && <StatusBadge status={rfq.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/rfqs') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : []),
          ...(canSend ? [{ key: 'send', label: t('rfqs.send'), onClick: () => { if (checkAttachmentMandatory()) runAction(() => sendMutation.mutateAsync(), t('rfqs.sendSuccess')); } }] : []),
          ...(canAward ? [{ key: 'award', label: t('rfqs.award'), onClick: () => runAction(() => awardMutation.mutateAsync(), t('rfqs.awardSuccess')) }] : [])
        ]}
        destructive={
          canCancel
            ? [{ key: 'cancel', label: t('rfqs.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('rfqs.cancelSuccess')), confirmMessage: t('rfqs.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('rfqNumber', t('rfqs.rfqNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (rfq?.rfqNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('rfqDate', t('rfqs.rfqDate'))}>
                <Input type="date" disabled={!isEditable} {...register('rfqDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('requiredDate', t('rfqs.requiredDate'))}>
                <Input type="date" disabled={!isEditable} {...register('requiredDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('suppliers', t('rfqs.suppliers'))}>
                <Controller
                  control={control}
                  name="supplierIds"
                  render={({ field }) => (
                    <SearchableMultiSelect disabled={!isEditable} style={{ minWidth: 260 }} value={field.value} onChange={(v) => field.onChange(v.map(Number))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('rfqs.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('rfqs.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('rfqs.unit'))}</th>
                    {isEditable && <th />}
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
                              disabled={!isEditable}
                              style={{ width: 220 }}
                              value={field.value}
                              onChange={(v) => field.onChange(Number(v))}
                              options={[{ value: 0, label: t('rfqs.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isEditable} style={{ width: 100 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field }) => (
                            <SearchableSelect
                              disabled={!isEditable}
                              style={{ width: 140 }}
                              value={field.value}
                              onChange={(v) => field.onChange(Number(v))}
                              options={[{ value: 0, label: t('rfqs.selectUnit') }, ...unitOptions]}
                            />
                          )}
                        />
                      </td>
                      {isEditable && (
                        <td>
                          <Button type="button" variant="ghost" onClick={() => remove(index)}>{t('common.remove')}</Button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {isEditable && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('rfqs.addLine')}
              </Button>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('rfqs.notes'))}>
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

      {rfq && !isNew && (canQuote || rfq.status === 'Awarded') && (
        <QuoteComparisonCard
          rfqId={rfq.id}
          lines={rfq.lines}
          suppliers={rfq.suppliers}
          canQuote={canQuote}
          onDecline={(rfqSupplierId) => runAction(() => declineMutation.mutateAsync(rfqSupplierId), t('rfqs.declineSuccess'))}
          onSelect={(quoteId) => runAction(() => selectMutation.mutateAsync(quoteId), t('rfqs.selectSuccess'))}
        />
      )}

      {showAttachments && !isNew && <AttachmentPanel entityType="RFQ" entityId={rfqId} />}
    </div>
  );
}

function QuoteComparisonCard({
  rfqId, lines, suppliers, canQuote, onDecline, onSelect
}: {
  rfqId: number;
  lines: { id: number; itemCode: string; itemNameAr: string }[];
  suppliers: RFQSupplier[];
  canQuote: boolean;
  onDecline: (rfqSupplierId: number) => void;
  onSelect: (quoteId: number) => void;
}) {
  const { t } = useTranslation();
  const setQuoteMutation = useSetRFQSupplierQuote(rfqId);
  const showToast = useToastStore((s) => s.show);
  const [drafts, setDrafts] = useState<Record<string, { unitPrice: string; discountPercentage: string; deliveryDays: string; validUntil: string }>>({});

  const draftKey = (rfqSupplierId: number, rfqLineId: number) => `${rfqSupplierId}-${rfqLineId}`;

  const getDraft = (rfqSupplierId: number, rfqLineId: number, existing?: { unitPrice: number; discountPercentage?: number; deliveryDays?: number; validUntil: string }) => {
    const key = draftKey(rfqSupplierId, rfqLineId);
    return drafts[key] ?? {
      unitPrice: existing?.unitPrice?.toString() ?? '',
      discountPercentage: existing?.discountPercentage?.toString() ?? '',
      deliveryDays: existing?.deliveryDays?.toString() ?? '',
      validUntil: existing?.validUntil ?? ''
    };
  };

  const setDraft = (rfqSupplierId: number, rfqLineId: number, patch: Partial<{ unitPrice: string; discountPercentage: string; deliveryDays: string; validUntil: string }>) => {
    const key = draftKey(rfqSupplierId, rfqLineId);
    setDrafts((prev) => ({ ...prev, [key]: { ...getDraft(rfqSupplierId, rfqLineId), ...prev[key], ...patch } }));
  };

  const saveQuote = async (rfqSupplierId: number, rfqLineId: number) => {
    const draft = getDraft(rfqSupplierId, rfqLineId);
    if (!draft.unitPrice || !draft.validUntil) {
      showToast(t('rfqs.quoteRequiredFields'), 'error');
      return;
    }
    try {
      await setQuoteMutation.mutateAsync({
        rfqSupplierId, rfqLineId,
        unitPrice: Number(draft.unitPrice),
        discountPercentage: draft.discountPercentage ? Number(draft.discountPercentage) : undefined,
        deliveryDays: draft.deliveryDays ? Number(draft.deliveryDays) : undefined,
        validUntil: draft.validUntil
      });
      showToast(t('rfqs.quoteSaved'), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <Card style={{ minWidth: 0 }}>
      <CardBody>
        <h3 style={{ marginTop: 0, fontSize: 15 }}>{t('rfqs.quoteComparison')}</h3>
        {lines.map((line) => (
          <div key={line.id} style={{ marginBottom: 20 }}>
            <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8 }}>{line.itemCode} — {line.itemNameAr}</div>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.supplier')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.responseStatus')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.unitPrice')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.discountPercentage')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.deliveryDays')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }}>{t('rfqs.validUntil')}</th>
                    <th style={{ textAlign: 'start', padding: 8 }} />
                  </tr>
                </thead>
                <tbody>
                  {suppliers.map((supplier) => {
                    const quote = supplier.quotes.find((q) => q.rfqLineId === line.id);
                    const draft = getDraft(supplier.id, line.id, quote);
                    const editable = canQuote && supplier.status !== 'Declined';
                    return (
                      <tr key={supplier.id} style={{ background: quote?.isSelected ? 'var(--color-success-bg)' : undefined }}>
                        <td style={{ padding: 8 }}>{supplier.supplierCode} — {supplier.supplierNameAr}</td>
                        <td style={{ padding: 8 }}><StatusBadge status={supplier.status} /></td>
                        <td style={{ padding: 8 }}>
                          <Input type="number" step="0.01" disabled={!editable} style={{ width: 90 }}
                            value={draft.unitPrice} onChange={(e) => setDraft(supplier.id, line.id, { unitPrice: e.target.value })} />
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input type="number" step="0.01" disabled={!editable} style={{ width: 80 }}
                            value={draft.discountPercentage} onChange={(e) => setDraft(supplier.id, line.id, { discountPercentage: e.target.value })} />
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input type="number" disabled={!editable} style={{ width: 70 }}
                            value={draft.deliveryDays} onChange={(e) => setDraft(supplier.id, line.id, { deliveryDays: e.target.value })} />
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input type="date" disabled={!editable} style={{ width: 140 }}
                            value={draft.validUntil} onChange={(e) => setDraft(supplier.id, line.id, { validUntil: e.target.value })} />
                        </td>
                        <td style={{ padding: 8, display: 'flex', gap: 4 }}>
                          {editable && (
                            <Button type="button" variant="ghost" onClick={() => saveQuote(supplier.id, line.id)}>{t('rfqs.saveQuote')}</Button>
                          )}
                          {canQuote && quote && !quote.isSelected && (
                            <Button type="button" variant="ghost" disabled={quote.isExpired} onClick={() => onSelect(quote.id)}>
                              {quote.isExpired ? t('rfqs.expired') : t('rfqs.select')}
                            </Button>
                          )}
                          {quote?.isSelected && <span style={{ color: 'var(--color-success)', fontWeight: 700 }}>{t('rfqs.winner')}</span>}
                          {canQuote && supplier.status === 'Pending' && (
                            <Button type="button" variant="ghost" onClick={() => onDecline(supplier.id)}>{t('rfqs.decline')}</Button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        ))}
      </CardBody>
    </Card>
  );
}
