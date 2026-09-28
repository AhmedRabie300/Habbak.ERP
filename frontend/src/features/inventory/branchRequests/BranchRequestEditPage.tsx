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
import { useItemsList } from '../items/api';
import { UnitSelect, defaultUnitId } from '../items/UnitSelect';
import { useBranchRequest, useCreateBranchRequest, useSubmitBranchRequest, useUpdateBranchRequest } from './api';
import type { BranchRequestLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface LineFormValues extends BranchRequestLineInput {
  minRequestQuantity?: number;
  maxRequestQuantity?: number;
}

interface FormValues {
  branchId: number | '';
  requestDate: string;
  lines: LineFormValues[];
}

const emptyLine = (): LineFormValues => ({ itemId: 0, requestedQuantity: 0 });

/** /inventory/branch-requests/:id — screen #12 (02-Module-Inventory-Manufacturing.md, section 5).
 * Rule 4 (RequestedQuantity vs BranchItemLimit) is enforced by SubmitBranchRequestCommand, not
 * here — this screen shows the limit inline once a line's known (loaded from an existing Draft's
 * GetById response) so the user sees it while typing, but a brand-new unsaved line has no limit
 * data to show yet; the hard check still runs server-side at Submit either way. */
export function BranchRequestEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const requestId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: request, isLoading } = useBranchRequest(requestId);
  const { data: branches } = useBranchesList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('INVENTORY_BRANCH_REQUEST');

  const { register, control, handleSubmit, reset, setValue, watch } = useForm<FormValues>({
    defaultValues: {
      branchId: '',
      requestDate: todayLocal(),
      lines: [emptyLine()]
    }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  const watchedLines = watch('lines');
  const itemsById = new Map((items ?? []).map((i) => [i.id, i]));

  // Remarks3 item 12: the branch list holds only the branches the user may work in (the branch data
  // scope) — a user tied to one branch gets it filled in.
  useEffect(() => {
    if (isNew && branches?.length === 1) setValue('branchId', branches[0].id);
  }, [isNew, branches, setValue]);

  useEffect(() => {
    if (request) {
      reset({
        branchId: request.branchId ?? '',
        requestDate: request.requestDate,
        lines: request.lines.map((l) => ({
          itemId: l.itemId,
          requestedQuantity: l.requestedQuantity,
          unitId: l.unitId,
          minRequestQuantity: l.minRequestQuantity,
          maxRequestQuantity: l.maxRequestQuantity
        }))
      });
    }
  }, [request, reset]);

  const createMutation = useCreateBranchRequest();
  const updateMutation = useUpdateBranchRequest(requestId ?? 0);
  const submitMutation = useSubmitBranchRequest(requestId ?? 0);

  const isEditable = isNew || request?.status === 'Draft';
  const branchOptions = (branches ?? []).map((b) => ({ value: b.id, label: `${b.code} — ${b.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

  const onSubmit = handleSubmit(async (values) => {
    const lines = values.lines.map((l) => ({ itemId: Number(l.itemId), requestedQuantity: Number(l.requestedQuantity), unitId: l.unitId ? Number(l.unitId) : undefined }));

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({ branchId: Number(values.branchId), requestDate: values.requestDate, lines });
        showToast(t('branchRequests.createSuccess'), 'success');
        navigate(`/inventory/branch-requests/${newId}`);
      } else if (request) {
        await updateMutation.mutateAsync({ rowVersion: request.rowVersion, requestDate: values.requestDate, lines });
        showToast(t('branchRequests.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handleSubmitForApproval = async () => {
    try {
      await submitMutation.mutateAsync();
      showToast(t('branchRequests.submitSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('branchRequests.addBranchRequest') : `${t('branchRequests.title')} — ${request?.requestNumber ?? ''}`}</h2>
        {request && <StatusBadge status={request.status} />}
      </div>

      <ActionBar
        primary={isEditable ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSubmit() } : undefined}
        secondary={[
          { key: 'back', label: isEditable ? t('common.cancel') : t('common.back'), onClick: () => navigate('/inventory/branch-requests') },
          ...(!isNew && request?.status === 'Draft' ? [{ key: 'submit', label: t('branchRequests.submitForApproval'), onClick: handleSubmitForApproval }] : [])
        ]}
      />

      <form onSubmit={onSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('requestNumber', t('branchRequests.requestNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (request?.requestNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('branch', t('branchRequests.branch'))}>
                <Controller
                  control={control}
                  name="branchId"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isNew}
                      style={{ minWidth: 200 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v === '' ? '' : Number(v))}
                      options={branchOptions}
                    />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('requestDate', t('branchRequests.requestDate'))}>
                <Input type="date" disabled={!isEditable} {...register('requestDate')} />
              </FieldWrapper>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('branchRequests.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('branchRequests.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('requestedQuantity', t('branchRequests.requestedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('limit', t('branchRequests.allowedRange'))}</th>
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
                              onChange={(v) => {
                                field.onChange(Number(v));
                                setValue(`lines.${index}.unitId`, defaultUnitId(itemsById.get(Number(v))));
                              }}
                              options={[{ value: 0, label: t('branchRequests.selectItem') }, ...itemOptions]}
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
                              item={itemsById.get(Number(watchedLines[index]?.itemId))}
                              value={unitField.value ?? defaultUnitId(itemsById.get(Number(watchedLines[index]?.itemId)))}
                              onChange={unitField.onChange}
                              disabled={!isEditable}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isEditable} style={{ width: 110 }} {...register(`lines.${index}.requestedQuantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8, color: 'var(--color-text-muted)', whiteSpace: 'nowrap' }}>
                        {field.minRequestQuantity != null || field.maxRequestQuantity != null
                          ? `${field.minRequestQuantity ?? 0} — ${field.maxRequestQuantity ?? '∞'} ${t('branchRequests.inBaseUnit')}`
                          : t('branchRequests.noLimit')}
                      </td>
                      {isEditable && (
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

            {isEditable && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('branchRequests.addLine')}
              </Button>
            )}
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
