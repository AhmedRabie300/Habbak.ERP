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
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useMandatorySettings } from '../../settings/codingRules/useMandatorySettings';
import { useSuppliersList } from '../suppliers/api';
import { useItemsList } from '../../inventory/items/api';
import { useCancelSupplierContract, useCreateSupplierContract, useSupplierContract, useUpdateSupplierContract } from './api';
import type { ContractItemInput } from './types';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  supplierId: number | '';
  startDate: string;
  endDate: string;
  autoRenew: boolean;
  notes: string;
  items: ContractItemInput[];
}

const emptyItem = (): ContractItemInput => ({ itemId: 0, unitPrice: 0 });

/** /purchasing/supplier-contracts/:id — screen #10. No draft/approval step — Active on creation,
 * freely editable while Active, Cancel ends it early (section 6 defines no state machine here). */
export function SupplierContractEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const contractId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [showAttachments, setShowAttachments] = useState(false);

  const { data: contract, isLoading } = useSupplierContract(contractId);
  const { data: suppliers } = useSuppliersList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_CONTRACT');
  const { checkDescriptionMandatory, checkAttachmentMandatory } = useMandatorySettings('PURCHASING_SUPPLIER_CONTRACT', 'SupplierContract', contractId);

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: {
      supplierId: '', startDate: todayLocal(), endDate: todayLocal(),
      autoRenew: false, notes: '', items: [emptyItem()]
    }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'items' });

  useEffect(() => {
    if (contract) {
      reset({
        supplierId: contract.supplierId,
        startDate: contract.startDate,
        endDate: contract.endDate,
        autoRenew: contract.autoRenew,
        notes: contract.notes ?? '',
        items: contract.items.map((i) => ({
          itemId: i.itemId, unitPrice: i.unitPrice, minQuantity: i.minQuantity, maxQuantity: i.maxQuantity, discountPercentage: i.discountPercentage
        }))
      });
    }
  }, [contract, reset]);

  const createMutation = useCreateSupplierContract();
  const updateMutation = useUpdateSupplierContract(contractId ?? 0);
  const cancelMutation = useCancelSupplierContract(contractId ?? 0);

  const isEditable = isNew || contract?.status === 'Active';
  const canCancel = !isNew && contract?.status === 'Active';

  const supplierOptions = (suppliers ?? []).map((s) => ({ value: s.id, label: `${s.code} — ${s.nameAr}` }));
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
    if (!checkDescriptionMandatory(values.notes)) return;
    if (!isNew && !checkAttachmentMandatory()) return;
    const contractItems = values.items.map((i) => ({
      itemId: Number(i.itemId), unitPrice: Number(i.unitPrice),
      minQuantity: i.minQuantity ? Number(i.minQuantity) : undefined,
      maxQuantity: i.maxQuantity ? Number(i.maxQuantity) : undefined,
      discountPercentage: i.discountPercentage ? Number(i.discountPercentage) : undefined
    }));
    const common = {
      supplierId: Number(values.supplierId),
      startDate: values.startDate,
      endDate: values.endDate,
      autoRenew: values.autoRenew,
      notes: values.notes || undefined,
      items: contractItems
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(common);
        showToast(t('supplierContracts.createSuccess'), 'success');
        navigate(`/purchasing/supplier-contracts/${newId}`);
      } else if (contract) {
        await updateMutation.mutateAsync({ ...common, rowVersion: contract.rowVersion });
        showToast(t('supplierContracts.updateSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('supplierContracts.addSupplierContract') : `${t('supplierContracts.title')} — ${contract?.contractNumber ?? ''}`}</h2>
        {contract && <StatusBadge status={contract.status} />}
      </div>

      <ActionBar
        primary={
          isEditable
            ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() }
            : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/purchasing/supplier-contracts') },
          ...(!isNew ? [{ key: 'attachments', label: t('attachments.title'), icon: 'paperclip', onClick: () => setShowAttachments((v) => !v) }] : [])
        ]}
        destructive={
          canCancel
            ? [{ key: 'cancel', label: t('supplierContracts.cancel'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('supplierContracts.cancelSuccess')), confirmMessage: t('supplierContracts.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('contractNumber', t('supplierContracts.contractNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (contract?.contractNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('supplier', t('supplierContracts.supplier'))}>
                <Controller
                  control={control}
                  name="supplierId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={supplierOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('startDate', t('supplierContracts.startDate'))}>
                <Input type="date" disabled={!isEditable} {...register('startDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('endDate', t('supplierContracts.endDate'))}>
                <Input type="date" disabled={!isEditable} {...register('endDate')} />
              </FieldWrapper>
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13.5, alignSelf: 'end', paddingBottom: 8 }}>
                <input type="checkbox" disabled={!isEditable} {...register('autoRenew')} />
                {label('autoRenew', t('supplierContracts.autoRenew'))}
              </label>
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('supplierContracts.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitPrice', t('supplierContracts.unitPrice'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('minQuantity', t('supplierContracts.minQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('maxQuantity', t('supplierContracts.maxQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('discountPercentage', t('supplierContracts.discountPercentage'))}</th>
                    {isEditable && <th />}
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td style={{ padding: 8 }}>
                        <Controller
                          control={control}
                          name={`items.${index}.itemId` as const}
                          render={({ field }) => (
                            <SearchableSelect
                              disabled={!isEditable}
                              style={{ width: 220 }}
                              value={field.value}
                              onChange={(v) => field.onChange(Number(v))}
                              options={[{ value: 0, label: t('supplierContracts.selectItem') }, ...itemOptions]}
                            />
                          )}
                        />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`items.${index}.unitPrice` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`items.${index}.minQuantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`items.${index}.maxQuantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.01" disabled={!isEditable} style={{ width: 100 }} {...register(`items.${index}.discountPercentage` as const, { valueAsNumber: true })} />
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
              <Button type="button" variant="ghost" onClick={() => append(emptyItem())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('supplierContracts.addItem')}
              </Button>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('supplierContracts.notes'))}>
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

      {showAttachments && !isNew && <AttachmentPanel entityType="SupplierContract" entityId={contractId} />}
    </div>
  );
}
