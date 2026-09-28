import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { Alert } from '../../../ui-kit/Alert';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useWarehousesList } from '../warehouses/api';
import { useItemsList } from '../items/api';
import { UnitSelect, defaultUnitId } from '../items/UnitSelect';
import { useCustodyOfficersList } from '../custodyOfficers/api';
import { useCreateWarehouseDocument, usePostWarehouseDocument, useUpdateWarehouseDocument, useWarehouseDocument } from './api';
import type { WarehouseDocumentKind, WarehouseDocumentLineInput } from './types';
import { todayLocal } from '../../../lib/date';

const KIND_META: Record<WarehouseDocumentKind, { titleKey: string; addKey: string; screenCode: string; warehouseRole: 'source' | 'destination' | 'adjustment' }> = {
  'stock-in': { titleKey: 'warehouseDocuments.stockInTitle', addKey: 'warehouseDocuments.addStockIn', screenCode: 'INVENTORY_STOCK_IN', warehouseRole: 'destination' },
  'stock-out': { titleKey: 'warehouseDocuments.stockOutTitle', addKey: 'warehouseDocuments.addStockOut', screenCode: 'INVENTORY_STOCK_OUT', warehouseRole: 'source' },
  'transfer-order': { titleKey: 'warehouseDocuments.transferOrderTitle', addKey: 'warehouseDocuments.addTransferOrder', screenCode: 'INVENTORY_TRANSFER_ORDER', warehouseRole: 'source' },
  'inventory-adjustments': { titleKey: 'warehouseDocuments.inventoryAdjustmentTitle', addKey: 'warehouseDocuments.addInventoryAdjustment', screenCode: 'INVENTORY_ADJUSTMENT', warehouseRole: 'adjustment' },
  'opening-balances': { titleKey: 'warehouseDocuments.openingBalanceTitle', addKey: 'warehouseDocuments.addOpeningBalance', screenCode: 'INVENTORY_OPENING_BALANCES', warehouseRole: 'destination' }
};

interface FormValues {
  documentDate: string;
  warehouseId: number | '';
  custodyOfficerId: number | '';
  adjustmentDirection: 'increase' | 'decrease';
  notes: string;
  lines: WarehouseDocumentLineInput[];
}

const emptyLine = (): WarehouseDocumentLineInput => ({ itemId: 0, quantity: 0, unitCost: 0, unitId: undefined, batchNumber: undefined, expiryDate: undefined });

/** Shared Edit screen for /inventory/stock-in, /inventory/stock-out, /inventory/transfer-order and
 * /inventory/inventory-adjustments — mirrors JournalEntryEditPage's Lines-editor + Draft/Posted
 * lifecycle pattern. A single warehouse field plays the role of destination (Stock In) or source
 * (Stock Out/Transfer Order); Transfer Order additionally requires a custody officer (rule 30).
 * InventoryAdjustment (screen #15) resolves its own direction per-document rather than by kind —
 * an increase/decrease toggle picks which of destination/source the single warehouse field fills. */
export function WarehouseDocumentEditPage({ kind }: { kind: WarehouseDocumentKind }) {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const documentId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const meta = KIND_META[kind];
  const isAdjustment = meta.warehouseRole === 'adjustment';
  const isTransferOrder = kind === 'transfer-order';

  const { data: document, isLoading } = useWarehouseDocument(kind, documentId);
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();
  const { data: custodyOfficers } = useCustodyOfficersList();
  const { label } = useFieldLabels(meta.screenCode);

  const title = t(meta.titleKey);
  const addLabel = t(meta.addKey);

  const { register, control, handleSubmit, reset, watch, setValue } = useForm<FormValues>({
    defaultValues: {
      documentDate: todayLocal(),
      warehouseId: '',
      custodyOfficerId: '',
      adjustmentDirection: 'increase',
      notes: '',
      lines: [emptyLine()]
    }
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });
  const adjustmentDirection = watch('adjustmentDirection');
  const watchedLines = watch('lines');
  // Remarks3 item 5: nothing to save without at least one line, and every line needs an item and a
  // quantity above zero — the server refuses the same.
  const linesValid = watchedLines.length > 0 && watchedLines.every((l) => Number(l.itemId) > 0 && Number(l.quantity) > 0);
  const itemsById = new Map((items ?? []).map((i) => [i.id, i]));
  const isDestination = isAdjustment ? adjustmentDirection === 'increase' : meta.warehouseRole === 'destination';

  useEffect(() => {
    if (document) {
      reset({
        documentDate: document.documentDate,
        warehouseId: (document.destinationWarehouseId ?? document.sourceWarehouseId) ?? '',
        custodyOfficerId: document.custodyOfficerId ?? '',
        adjustmentDirection: document.destinationWarehouseId != null ? 'increase' : 'decrease',
        notes: document.notes ?? '',
        lines: document.lines.map((l) => ({
          itemId: l.itemId,
          quantity: l.quantity,
          unitCost: l.unitCost,
          unitId: l.unitId,
          batchNumber: l.batchNumber,
          expiryDate: l.expiryDate
        }))
      });
    }
  }, [document, reset]);

  const createMutation = useCreateWarehouseDocument(kind);
  const updateMutation = useUpdateWarehouseDocument(kind, documentId ?? 0);
  const postMutation = usePostWarehouseDocument(kind, documentId ?? 0);

  const isEditable = isNew || document?.status === 'Draft';
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const custodyOfficerOptions = (custodyOfficers ?? []).map((c) => ({ value: c.id, label: `${c.code} — ${c.nameAr}` }));

  const onSubmit = handleSubmit(async (values) => {
    if (!linesValid) {
      showToast(t('warehouseDocuments.linesInvalid'), 'error');
      return;
    }

    const payload = {
      documentDate: values.documentDate,
      sourceWarehouseId: isDestination ? undefined : (values.warehouseId === '' ? undefined : Number(values.warehouseId)),
      destinationWarehouseId: isDestination ? (values.warehouseId === '' ? undefined : Number(values.warehouseId)) : undefined,
      custodyOfficerId: isTransferOrder ? (values.custodyOfficerId === '' ? undefined : Number(values.custodyOfficerId)) : undefined,
      notes: values.notes || undefined,
      lines: values.lines.map((l) => ({
        itemId: Number(l.itemId),
        quantity: Number(l.quantity),
        unitCost: Number(l.unitCost),
        unitId: l.unitId ? Number(l.unitId) : undefined,
        batchNumber: l.batchNumber || undefined,
        expiryDate: l.expiryDate || undefined
      }))
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('warehouseDocuments.createSuccess'), 'success');
        navigate(`/inventory/${kind}/${newId}`);
      } else if (document) {
        await updateMutation.mutateAsync({ ...payload, rowVersion: document.rowVersion });
        showToast(t('warehouseDocuments.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const handlePost = async () => {
    try {
      await postMutation.mutateAsync();
      showToast(t('warehouseDocuments.postSuccess'), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? addLabel : `${title} — ${document?.documentNumber ?? ''}`}</h2>
        {document && <StatusBadge status={document.status} />}
      </div>

      <ActionBar
        primary={
          document?.status === 'Draft'
            ? { key: 'post', label: t('common.post'), onClick: handlePost }
            : isEditable
              ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSubmit(), disabled: !linesValid }
              : undefined
        }
        secondary={[
          { key: 'back', label: isEditable ? t('common.cancel') : t('common.back'), onClick: () => navigate(`/inventory/${kind}`) },
          ...(document?.status === 'Draft'
            ? [{ key: 'saveChanges', label: t('common.saveChanges'), onClick: () => onSubmit(), disabled: !linesValid }]
            : [])
        ]}
      />

      {document?.status === 'Posted' && <Alert tone="warning">{t('warehouseDocuments.lockedPosted')}</Alert>}

      <form onSubmit={onSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('documentNumber', t('warehouseDocuments.documentNumber'))}>
                <Input value={isNew ? t('codingRules.autoGeneratedPlaceholder') : (document?.documentNumber ?? '')} disabled />
              </FieldWrapper>
              <FieldWrapper label={label('documentDate', t('warehouseDocuments.documentDate'))}>
                <Input type="date" disabled={!isEditable} {...register('documentDate')} />
              </FieldWrapper>
              {isAdjustment && (
                <FieldWrapper label={label('direction', t('warehouseDocuments.adjustmentDirection'))}>
                  <Controller
                    control={control}
                    name="adjustmentDirection"
                    render={({ field }) => (
                      <SearchableSelect
                        disabled={!isEditable}
                        style={{ minWidth: 160 }}
                        value={field.value}
                        onChange={(v) => field.onChange(v)}
                        options={[
                          { value: 'increase', label: t('warehouseDocuments.adjustmentIncrease') },
                          { value: 'decrease', label: t('warehouseDocuments.adjustmentDecrease') }
                        ]}
                      />
                    )}
                  />
                </FieldWrapper>
              )}
              <FieldWrapper label={label('warehouse', isDestination ? t('warehouseDocuments.destinationWarehouse') : t('warehouseDocuments.sourceWarehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect
                      disabled={!isEditable}
                      style={{ minWidth: 200 }}
                      value={field.value}
                      onChange={(v) => field.onChange(v === '' ? '' : Number(v))}
                      options={warehouseOptions}
                    />
                  )}
                />
              </FieldWrapper>
              {isTransferOrder && (
                <FieldWrapper label={label('custodyOfficer', t('warehouseDocuments.custodyOfficer'))}>
                  <Controller
                    control={control}
                    name="custodyOfficerId"
                    render={({ field }) => (
                      <SearchableSelect
                        disabled={!isEditable}
                        style={{ minWidth: 200 }}
                        value={field.value}
                        onChange={(v) => field.onChange(v === '' ? '' : Number(v))}
                        options={custodyOfficerOptions}
                      />
                    )}
                  />
                </FieldWrapper>
              )}
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('warehouseDocuments.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('warehouseDocuments.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('warehouseDocuments.quantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unitCost', t('warehouseDocuments.unitCostPerUnit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('warehouseDocuments.batchNumber'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('expiryDate', t('warehouseDocuments.expiryDate'))}</th>
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
                                // A newly picked item starts in its base unit.
                                setValue(`lines.${index}.unitId`, defaultUnitId(itemsById.get(Number(v))));
                              }}
                              options={[{ value: 0, label: t('warehouseDocuments.selectItem') }, ...itemOptions]}
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
                        <Input type="number" step="0.0001" min="0" disabled={!isEditable} style={{ width: 110 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="number" step="0.0001" disabled={!isEditable} style={{ width: 110 }} {...register(`lines.${index}.unitCost` as const, { valueAsNumber: true })} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input disabled={!isEditable} style={{ width: 130 }} {...register(`lines.${index}.batchNumber` as const)} />
                      </td>
                      <td style={{ padding: 8 }}>
                        <Input type="date" disabled={!isEditable} style={{ width: 150 }} {...register(`lines.${index}.expiryDate` as const)} />
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

            {isEditable && !linesValid && (
              <div style={{ marginTop: 8 }}><Alert tone="warning">{t('warehouseDocuments.linesInvalid')}</Alert></div>
            )}

            {isEditable && (
              <Button type="button" variant="ghost" onClick={() => append(emptyLine())} style={{ alignSelf: 'flex-start', marginTop: 8 }}>
                {t('warehouseDocuments.addLine')}
              </Button>
            )}

            <div style={{ marginTop: 16, width: '100%' }}>
              <FieldWrapper label={label('notes', t('warehouseDocuments.notes'))}>
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
