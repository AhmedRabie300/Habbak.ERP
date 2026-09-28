import { useEffect, useState } from 'react';
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
import { useSuppliersList } from '../suppliers/api';
import { useCreateSupplierEvaluation, useDeleteSupplierEvaluation, useSupplierEvaluation, useUpdateSupplierEvaluation } from './api';
import type { SupplierEvaluationFormValues } from './types';
import { todayLocal } from '../../../lib/date';

/** /purchasing/supplier-evaluations/:id — screen #12. No workflow — always editable, same as
 * Supplier itself (this is a historical record, corrected in place rather than reversed). */
export function SupplierEvaluationEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const evaluationId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  // Fix for the spurious "not found" toast on delete: once the delete mutation starts, this page
  // stops asking for the evaluation at all (rather than relying on cache invalidation timing) —
  // the query would otherwise still be `enabled` while this component is mounted mid-navigate-
  // away, see it's now missing from the cache, and refetch straight into a 404.
  const [isDeleted, setIsDeleted] = useState(false);
  const { data: evaluation, isLoading } = useSupplierEvaluation(isDeleted ? undefined : evaluationId);
  const { data: suppliers } = useSuppliersList();
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_EVALUATION');

  const { register, control, handleSubmit, reset, watch } = useForm<SupplierEvaluationFormValues>({
    defaultValues: {
      supplierId: 0, evaluationDate: todayLocal(),
      qualityScore: 0, deliveryTimeScore: 0, quantityComplianceScore: 0, notes: ''
    }
  });

  useEffect(() => {
    if (evaluation) {
      reset({
        supplierId: evaluation.supplierId,
        evaluationDate: evaluation.evaluationDate,
        qualityScore: evaluation.qualityScore,
        deliveryTimeScore: evaluation.deliveryTimeScore,
        quantityComplianceScore: evaluation.quantityComplianceScore,
        notes: evaluation.notes ?? ''
      });
    }
  }, [evaluation, reset]);

  const createMutation = useCreateSupplierEvaluation();
  const updateMutation = useUpdateSupplierEvaluation(evaluationId ?? 0);
  const deleteMutation = useDeleteSupplierEvaluation();

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
  const [qualityScore, deliveryTimeScore, quantityComplianceScore] = watch(['qualityScore', 'deliveryTimeScore', 'quantityComplianceScore']);
  const overallPreview = ((Number(qualityScore) || 0) + (Number(deliveryTimeScore) || 0) + (Number(quantityComplianceScore) || 0)) / 3;

  const onSave = handleSubmit(async (values) => {
    const common = {
      supplierId: Number(values.supplierId),
      evaluationDate: values.evaluationDate,
      qualityScore: Number(values.qualityScore),
      deliveryTimeScore: Number(values.deliveryTimeScore),
      quantityComplianceScore: Number(values.quantityComplianceScore),
      notes: values.notes || undefined
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('supplierEvaluations.createSuccess'), 'success');
        navigate(`/purchasing/supplier-evaluations/${newId}`);
      } else if (evaluation) {
        await updateMutation.mutateAsync({ ...common, rowVersion: evaluation.rowVersion });
        showToast(t('supplierEvaluations.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleDelete = async () => {
    if (!evaluationId) return;
    setIsDeleted(true);
    try {
      await deleteMutation.mutateAsync(evaluationId);
      showToast(t('supplierEvaluations.deleteSuccess'), 'success');
      navigate('/purchasing/supplier-evaluations');
    } catch (error) {
      setIsDeleted(false);
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
        <h2 style={{ margin: 0 }}>{isNew ? t('supplierEvaluations.addSupplierEvaluation') : t('supplierEvaluations.title')}</h2>
      </div>

      <ActionBar
        primary={{ key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/supplier-evaluations') }]}
        destructive={
          !isNew
            ? [{ key: 'delete', label: t('common.remove'), onClick: handleDelete, confirmMessage: t('supplierEvaluations.deleteConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('supplier', t('supplierEvaluations.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('evaluationDate', t('supplierEvaluations.evaluationDate'))}>
                <Input type="date" {...register('evaluationDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('qualityScore', t('supplierEvaluations.qualityScore'))}>
                <Input type="number" step="0.1" min={0} max={100} style={{ width: 100 }} {...register('qualityScore', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('deliveryTimeScore', t('supplierEvaluations.deliveryTimeScore'))}>
                <Input type="number" step="0.1" min={0} max={100} style={{ width: 100 }} {...register('deliveryTimeScore', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('quantityComplianceScore', t('supplierEvaluations.quantityComplianceScore'))}>
                <Input type="number" step="0.1" min={0} max={100} style={{ width: 100 }} {...register('quantityComplianceScore', { valueAsNumber: true })} />
              </FieldWrapper>
            </div>

            <div style={{ marginTop: 16, fontSize: 13, fontWeight: 600 }}>
              {t('supplierEvaluations.overallScore')}: {overallPreview.toFixed(1)}
            </div>

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('supplierEvaluations.notes'))}>
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
