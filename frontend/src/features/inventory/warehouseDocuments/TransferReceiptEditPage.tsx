import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, useWatch, Controller } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
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
import {
  useCreateTransferReceipt,
  usePostedTransferOrdersList,
  usePostTransferReceipt,
  useTransferReceipt,
  useUpdateTransferReceipt,
  useWarehouseDocument
} from './api';
import type { TransferReceiptLineInput } from './types';
import { todayLocal } from '../../../lib/date';

interface LineFormValues extends TransferReceiptLineInput {
  itemCode: string;
  itemNameAr: string;
  expectedQuantity: number;
  /** Read-only: a receipt line is in the unit its transfer order line was shipped in. */
  unitNameAr?: string;
}

interface FormValues {
  relatedWarehouseDocumentId: number | '';
  documentDate: string;
  destinationWarehouseId: number | '';
  notes: string;
  lines: LineFormValues[];
}

/** /inventory/transfer-receipt/:id — screen #11, the module doc's one screen explicitly marked
 * "شاشة تفاعلية" (interactive, non-standard): picking a Posted TransferOrder loads its lines as
 * read-only Expected quantities, and VarianceQuantity (rule 7) recalculates live as the actual
 * received quantity is typed. */
export function TransferReceiptEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const documentId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: document, isLoading } = useTransferReceipt(documentId);
  const { data: postableOrders } = usePostedTransferOrdersList();
  const { data: warehouses } = useWarehousesList();
  const { label } = useFieldLabels('INVENTORY_TRANSFER_RECEIPT');

  const { register, control, handleSubmit, reset, setValue } = useForm<FormValues>({
    defaultValues: {
      relatedWarehouseDocumentId: '',
      documentDate: todayLocal(),
      destinationWarehouseId: '',
      notes: '',
      lines: []
    }
  });
  const { fields } = useFieldArray({ control, name: 'lines' });
  const watchedOrderId = useWatch({ control, name: 'relatedWarehouseDocumentId' });
  const watchedLines = useWatch({ control, name: 'lines' });

  const [loadedOrderId, setLoadedOrderId] = useState<number | ''>('');
  const { data: selectedOrder } = useWarehouseDocument('transfer-order', typeof watchedOrderId === 'number' ? watchedOrderId : undefined);

  // Loading a TransferOrder's lines happens once, right after the user picks it — not on every
  // render, since the user must still be free to edit the received quantities afterward.
  useEffect(() => {
    if (isNew && selectedOrder && watchedOrderId !== '' && watchedOrderId !== loadedOrderId) {
      setValue(
        'lines',
        selectedOrder.lines.map((l) => ({
          itemId: l.itemId,
          itemCode: l.itemCode,
          itemNameAr: l.itemNameAr,
          unitNameAr: l.unitNameAr,
          quantity: l.quantity,
          expectedQuantity: l.quantity,
          batchNumber: l.batchNumber,
          expiryDate: l.expiryDate
        }))
      );
      setLoadedOrderId(watchedOrderId);
    }
  }, [isNew, selectedOrder, watchedOrderId, loadedOrderId, setValue]);

  useEffect(() => {
    if (document) {
      reset({
        relatedWarehouseDocumentId: document.relatedWarehouseDocumentId ?? '',
        documentDate: document.documentDate,
        destinationWarehouseId: document.destinationWarehouseId ?? '',
        notes: document.notes ?? '',
        lines: document.lines.map((l) => ({
          itemId: l.itemId,
          itemCode: l.itemCode,
          itemNameAr: l.itemNameAr,
          unitNameAr: l.unitNameAr,
          quantity: l.quantity,
          expectedQuantity: l.expectedQuantity ?? 0,
          batchNumber: l.batchNumber,
          expiryDate: l.expiryDate
        }))
      });
    }
  }, [document, reset]);

  const createMutation = useCreateTransferReceipt();
  const updateMutation = useUpdateTransferReceipt(documentId ?? 0);
  const postMutation = usePostTransferReceipt(documentId ?? 0);

  const isEditable = isNew || document?.status === 'Draft';
  const orderOptions = (postableOrders ?? []).map((o) => ({ value: o.id, label: `${o.documentNumber} — ${o.sourceWarehouseCode} (${o.custodyOfficerCode})` }));
  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));

  const onSubmit = handleSubmit(async (values) => {
    const lines = values.lines.map((l) => ({
      itemId: l.itemId,
      quantity: Number(l.quantity),
      batchNumber: l.batchNumber || undefined,
      expiryDate: l.expiryDate || undefined
    }));

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync({
          relatedWarehouseDocumentId: Number(values.relatedWarehouseDocumentId),
          documentDate: values.documentDate,
          destinationWarehouseId: Number(values.destinationWarehouseId),
          notes: values.notes || undefined,
          lines
        });
        showToast(t('warehouseDocuments.createSuccess'), 'success');
        navigate(`/inventory/transfer-receipt/${newId}`);
      } else if (document) {
        await updateMutation.mutateAsync({
          rowVersion: document.rowVersion,
          documentDate: values.documentDate,
          destinationWarehouseId: Number(values.destinationWarehouseId),
          notes: values.notes || undefined,
          lines
        });
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
        <h2 style={{ margin: 0 }}>{isNew ? t('warehouseDocuments.addTransferReceipt') : `${t('warehouseDocuments.transferReceiptTitle')} — ${document?.documentNumber ?? ''}`}</h2>
        {document && <StatusBadge status={document.status} />}
      </div>

      <ActionBar
        primary={
          document?.status === 'Draft'
            ? { key: 'post', label: t('common.post'), onClick: handlePost }
            : isEditable
              ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSubmit() }
              : undefined
        }
        secondary={[
          { key: 'back', label: isEditable ? t('common.cancel') : t('common.back'), onClick: () => navigate('/inventory/transfer-receipt') },
          ...(document?.status === 'Draft'
            ? [{ key: 'saveChanges', label: t('common.saveChanges'), onClick: () => onSubmit() }]
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
              <FieldWrapper label={label('relatedOrder', t('warehouseDocuments.relatedTransferOrder'))}>
                {isNew ? (
                  <Controller
                    control={control}
                    name="relatedWarehouseDocumentId"
                    render={({ field }) => (
                      <SearchableSelect
                        value={field.value}
                        onChange={(v) => field.onChange(v === '' ? '' : Number(v))}
                        options={orderOptions}
                        style={{ minWidth: 240 }}
                      />
                    )}
                  />
                ) : (
                  <Input value={document?.relatedWarehouseDocumentNumber ?? ''} disabled style={{ minWidth: 200 }} />
                )}
              </FieldWrapper>
              <FieldWrapper label={label('documentDate', t('warehouseDocuments.documentDate'))}>
                <Input type="date" disabled={!isEditable} {...register('documentDate')} />
              </FieldWrapper>
              <FieldWrapper label={label('destinationWarehouse', t('warehouseDocuments.destinationWarehouse'))}>
                <Controller
                  control={control}
                  name="destinationWarehouseId"
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
            </div>

            <div style={{ overflowX: 'auto', marginTop: 16 }}>
              <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                  <tr>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('item', t('warehouseDocuments.item'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('unit', t('warehouseDocuments.unit'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('expectedQuantity', t('warehouseDocuments.expectedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('quantity', t('warehouseDocuments.receivedQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('varianceQuantity', t('warehouseDocuments.varianceQuantity'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('batchNumber', t('warehouseDocuments.batchNumber'))}</th>
                    <th style={{ textAlign: 'start', padding: 8, whiteSpace: 'nowrap' }}>{label('expiryDate', t('warehouseDocuments.expiryDate'))}</th>
                  </tr>
                </thead>
                <tbody>
                  {fields.length === 0 && (
                    <tr>
                      <td colSpan={7} style={{ padding: 8, color: 'var(--color-text-muted)' }}>
                        {t('warehouseDocuments.pickOrderFirst')}
                      </td>
                    </tr>
                  )}
                  {fields.map((field, index) => {
                    const expected = watchedLines?.[index]?.expectedQuantity ?? field.expectedQuantity;
                    const actual = Number(watchedLines?.[index]?.quantity ?? field.quantity);
                    const variance = actual - Number(expected);
                    return (
                      <tr key={field.id}>
                        <td style={{ padding: 8 }}>{field.itemCode} — {field.itemNameAr}</td>
                        <td style={{ padding: 8 }}>{field.unitNameAr ?? '—'}</td>
                        <td style={{ padding: 8 }}>{expected.toLocaleString()}</td>
                        <td style={{ padding: 8 }}>
                          <Input type="number" step="0.0001" disabled={!isEditable} style={{ width: 110 }} {...register(`lines.${index}.quantity` as const, { valueAsNumber: true })} />
                        </td>
                        <td style={{ padding: 8, fontWeight: 700, color: variance === 0 ? 'var(--color-success)' : 'var(--color-error)' }}>
                          {variance.toLocaleString()}
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input disabled={!isEditable} style={{ width: 130 }} {...register(`lines.${index}.batchNumber` as const)} />
                        </td>
                        <td style={{ padding: 8 }}>
                          <Input type="date" disabled={!isEditable} style={{ width: 150 }} {...register(`lines.${index}.expiryDate` as const)} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

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
