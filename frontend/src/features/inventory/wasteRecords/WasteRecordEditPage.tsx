import { useEffect } from 'react';
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
import { useWarehousesList } from '../warehouses/api';
import { useItemsList } from '../items/api';
import { useCreateWasteRecord, useUpdateWasteRecord, useWasteRecord } from './api';
import { todayLocal } from '../../../lib/date';

interface FormValues {
  warehouseId: number | '';
  itemId: number | '';
  quantity: number;
  wasteDate: string;
  reason: string;
}

/** /inventory/waste-records/:id — screen #18. A record generated automatically from a completed
 * ProductionOrder (SourceDocumentType != Manual) is read-only here — editing it would desync the
 * displayed value from the stock movement it already traced. Even a Manual record only lets
 * WasteDate/Reason change once saved: Warehouse/Item/Quantity already posted a TransactionType.Waste
 * stock movement at creation time and have no reversal-and-reapply path on this screen. */
export function WasteRecordEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const recordId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: record, isLoading } = useWasteRecord(recordId);
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();
  const { label } = useFieldLabels('INVENTORY_WASTE_RECORD');

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { warehouseId: '', itemId: '', quantity: 0, wasteDate: todayLocal(), reason: '' }
  });

  useEffect(() => {
    if (record) {
      reset({
        warehouseId: record.warehouseId,
        itemId: record.itemId,
        quantity: record.quantity,
        wasteDate: record.wasteDate,
        reason: record.reason
      });
    }
  }, [record, reset]);

  const createMutation = useCreateWasteRecord();
  const updateMutation = useUpdateWasteRecord(recordId ?? 0);

  const isManual = isNew || record?.sourceDocumentType == null || record?.sourceDocumentType === 'Manual';
  // Warehouse/Item/Quantity are fixed once the record exists — only Manual's WasteDate/Reason can
  // still change (see the command's own doc comment for why).
  const canEditSubstance = isNew;
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

  const onSave = handleSubmit(async (values) => {
    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          warehouseId: Number(values.warehouseId),
          itemId: Number(values.itemId),
          quantity: Number(values.quantity),
          wasteDate: values.wasteDate,
          reason: values.reason
        });
        showToast(t('wasteRecords.createSuccess'), 'success');
        navigate(`/inventory/waste-records/${newId}`);
      } else if (record) {
        await updateMutation.mutateAsync({ rowVersion: record.rowVersion, wasteDate: values.wasteDate, reason: values.reason });
        showToast(t('wasteRecords.updateSuccess'), 'success');
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
      <h2 style={{ margin: 0 }}>{isNew ? t('wasteRecords.addWasteRecord') : t('wasteRecords.title')}</h2>

      <ActionBar
        primary={isManual ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/waste-records') }]}
      />

      <form onSubmit={onSave}>
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('warehouse', t('wasteRecords.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!canEditSubstance} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('item', t('wasteRecords.item'))}>
                <Controller
                  control={control}
                  name="itemId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!canEditSubstance} style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={itemOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('quantity', t('wasteRecords.quantity'))}>
                <Input type="number" step="0.0001" disabled={!canEditSubstance} style={{ width: 120 }} {...register('quantity', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('wasteDate', t('wasteRecords.wasteDate'))}>
                <Input type="date" disabled={!isManual} {...register('wasteDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('reason', t('wasteRecords.reason'))}>
                <Input disabled={!isManual} style={{ minWidth: 220 }} {...register('reason')} />
              </FieldWrapper>
            </div>
          </CardBody>
        </Card>
      </form>
    </div>
  );
}
