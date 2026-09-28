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
import { useItemsList } from '../items/api';
import {
  useApproveRecipe, useCreateNewRecipeVersion, useCreateRecipe, useRecipe, useRejectRecipe, useSubmitRecipe, useUpdateRecipe
} from './api';
import type { RecipeLineInput } from './types';
import { UnitSelect, defaultUnitId, unitFactor } from '../items/UnitSelect';
import { todayLocal } from '../../../lib/date';

interface LineFormValues extends RecipeLineInput {
  componentStandardCost?: number;
}

interface FormValues {
  outputItemId: number | '';
  outputQuantity: number;
  wastePercentage: number;
  effectiveFromDate: string;
  lines: LineFormValues[];
}

const emptyLine = (): LineFormValues => ({ componentItemId: 0, quantity: 0 });

/** /inventory/recipes/:id — screens #16-17 (02-Module-Inventory-Manufacturing.md, section 5).
 * Combines the standard Create/Update Draft screen (#16) with the approval screen (#17) in one
 * page, switching by Status: Draft shows editable fields + Submit, PendingApproval shows the same
 * data read-only + Approve/Reject, Approved shows a "Create New Version" action (rule 31) — no
 * extra inputs are needed for approval beyond what's already on screen, so a separate route would
 * just duplicate this same layout. */
export function RecipeEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const recipeId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: recipe, isLoading } = useRecipe(recipeId);
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('INVENTORY_RECIPE');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      outputItemId: '',
      outputQuantity: 0,
      wastePercentage: 0,
      effectiveFromDate: todayLocal(),
      lines: [emptyLine()]
    }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  const watchedLines = watch('lines');
  const itemsById = new Map((items ?? []).map((i) => [i.id, i]));

  useEffect(() => {
    if (recipe) {
      reset({
        outputItemId: recipe.outputItemId,
        outputQuantity: recipe.outputQuantity,
        wastePercentage: recipe.wastePercentage,
        effectiveFromDate: recipe.effectiveFromDate,
        lines: recipe.lines.map((l) => ({
          componentItemId: l.componentItemId,
          quantity: l.quantity,
          unitId: l.unitId,
          componentStandardCost: l.componentStandardCost
        }))
      });
    }
  }, [recipe, reset]);

  const createMutation = useCreateRecipe();
  const updateMutation = useUpdateRecipe(recipeId ?? 0);
  const submitMutation = useSubmitRecipe(recipeId ?? 0);
  const approveMutation = useApproveRecipe(recipeId ?? 0);
  const rejectMutation = useRejectRecipe(recipeId ?? 0);
  const newVersionMutation = useCreateNewRecipeVersion(recipeId ?? 0);

  const isDraft = isNew || recipe?.status === 'Draft';
  const isPendingApproval = recipe?.status === 'PendingApproval';
  const isApproved = recipe?.status === 'Approved';
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
    const lines = values.lines.map((l) => ({ componentItemId: Number(l.componentItemId), quantity: Number(l.quantity), unitId: l.unitId ? Number(l.unitId) : undefined }));

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          outputItemId: Number(values.outputItemId),
          outputQuantity: Number(values.outputQuantity),
          wastePercentage: Number(values.wastePercentage),
          effectiveFromDate: values.effectiveFromDate,
          lines
        });
        showToast(t('recipes.createSuccess'), 'success');
        navigate(`/inventory/recipes/${newId}`);
      } else if (recipe) {
        await updateMutation.mutateAsync({
          rowVersion: recipe.rowVersion,
          outputQuantity: Number(values.outputQuantity),
          wastePercentage: Number(values.wastePercentage),
          effectiveFromDate: values.effectiveFromDate,
          lines
        });
        showToast(t('recipes.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>
          {isNew ? t('recipes.addRecipe') : `${recipe?.recipeFamilyCode ?? ''} (v${recipe?.versionNumber ?? ''})`}
        </h2>
        {recipe && <StatusBadge status={recipe.status} />}
      </div>

      <ActionBar
        primary={
          isDraft
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : isPendingApproval
              ? { key: 'approve', label: t('recipes.approve'), onClick: () => runAction(() => approveMutation.mutateAsync(), t('recipes.approveSuccess')) }
              : isApproved
                ? { key: 'new-version', label: t('recipes.createNewVersion'), onClick: async () => {
                    try {
                      const { id: newId } = await newVersionMutation.mutateAsync();
                      showToast(t('recipes.newVersionSuccess'), 'success');
                      navigate(`/inventory/recipes/${newId}`);
                    } catch (error) {
                      const message = getFieldErrorMessage(error);
                      if (message) showToast(message, 'error');
                    }
                  } }
                : undefined
        }
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/recipes') }]}
        destructive={
          isPendingApproval
            ? [{ key: 'reject', label: t('recipes.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('recipes.rejectSuccess')), confirmMessage: t('recipes.rejectConfirm') }]
            : []
        }
      />

      {!isNew && recipe?.status === 'Draft' && (
        <ActionBar
          secondary={[{ key: 'submit', label: t('recipes.submitForApproval'), onClick: () => runAction(() => submitMutation.mutateAsync(), t('recipes.submitSuccess')) }]}
        />
      )}

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('outputItem', t('recipes.outputItem'))}>
                <Controller
                  control={control}
                  name="outputItemId"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isNew}
                      style={{ minWidth: 220 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v === '' ? '' : Number(v))}
                      options={itemOptions}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('outputQuantity', t('recipes.outputQuantity'))}>
                <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 120 }} {...register('outputQuantity', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('wastePercentage', t('recipes.wastePercentage'))}>
                <Input type="number" step="0.01" disabled={!isDraft} style={{ width: 100 }} {...register('wastePercentage', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('effectiveFromDate', t('recipes.effectiveFromDate'))}>
                <Input type="date" disabled={!isDraft} {...register('effectiveFromDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('component', t('recipes.component'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('recipes.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('recipes.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('componentCost', t('recipes.componentCost'))}</th>
                    {isDraft && <th />}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.componentItemId` as const}
                          render={({ field }) => (
                            <SearchableSelect
                              disabled={!isDraft}
                              style={{ width: 220 }}
                              value={field.value}
                              onChange={(v) => {
                                field.onChange(Number(v));
                                setValue(`lines.${index}.unitId`, defaultUnitId(itemsById.get(Number(v))));
                              }}
                              options={[{ value: 0, label: t('recipes.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`lines.${index}.unitId` as const}
                          render={({ field: unitField }) => (
                            <UnitSelect
                              item={itemsById.get(Number(watchedLines[index]?.componentItemId))}
                              value={unitField.value ?? defaultUnitId(itemsById.get(Number(watchedLines[index]?.componentItemId)))}
                              onChange={unitField.onChange}
                              disabled={!isDraft}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isDraft} style={{ width: 110 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8, color: 'var(--color-text-muted)' }}>
                        {/* Standard cost is per base unit; the line may be in a larger unit. */}
                        {field.componentStandardCost != null
                          ? (field.componentStandardCost * Number(watchedLines[index]?.quantity ?? field.quantity)
                            * unitFactor(itemsById.get(Number(watchedLines[index]?.componentItemId)), watchedLines[index]?.unitId)).toFixed(2)
                          : '—'}
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
                {t('recipes.addComponent')}
              </Button>
            )}

            {recipe && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('recipes.estimatedComponentCost')}: {recipe.estimatedComponentCost.toFixed(2)}</span>
                <span>{t('recipes.costPerOutputUnit')}: {recipe.costPerOutputUnit.toFixed(2)}</span>
              </div>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
