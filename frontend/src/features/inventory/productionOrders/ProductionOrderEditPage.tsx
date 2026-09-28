import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
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
import { useWarehousesList } from '../warehouses/api';
import { useRecipesList } from '../recipes/api';
import {
  useCancelProductionOrder, useCompleteProductionOrder, useCreateProductionOrder, useProductionOrder, useStartProductionOrder, useUpdateProductionOrder
} from './api';
import { todayLocal } from '../../../lib/date';
import { useButtonPermission } from '../../auth/access';

interface FormValues {
  warehouseId: number | '';
  recipeId: number | '';
  plannedQuantity: number;
  startDate: string;
}

/** /inventory/production-orders/:id — screen #19 (02-Module-Inventory-Manufacturing.md, section 5).
 * "إكمال الأمر" (rule 15) is a plain inline section shown only while InProgress, rather than a
 * separate modal/route — it needs no data this page hasn't already loaded. */
export function ProductionOrderEditPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const isNew = id === 'new';
  const orderId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: order, isLoading } = useProductionOrder(orderId);
  const { data: warehouses } = useWarehousesList();
  const { data: recipes } = useRecipesList({ pageSize: 200 });
  const { label } = useFieldLabels('INVENTORY_PRODUCTION_ORDER');

  const { register, control, handleSubmit, reset } = useForm<FormValues>({
    defaultValues: { warehouseId: '', recipeId: '', plannedQuantity: 0, startDate: todayLocal() }
  });

  useEffect(() => {
    if (order) {
      reset({
        warehouseId: order.warehouseId,
        recipeId: order.recipeId,
        plannedQuantity: order.plannedQuantity,
        startDate: order.startDate ?? ''
      });
    }
  }, [order, reset]);

  const createMutation = useCreateProductionOrder();
  const updateMutation = useUpdateProductionOrder(orderId ?? 0);
  const startMutation = useStartProductionOrder(orderId ?? 0);
  const cancelMutation = useCancelProductionOrder(orderId ?? 0);
  const completeMutation = useCompleteProductionOrder(orderId ?? 0);
  const canComplete = useButtonPermission('INVENTORY_PRODUCTION_ORDERS', 'Complete');

  const [actualQuantity, setActualQuantity] = useState('');
  const [endDate, setEndDate] = useState(todayLocal());
  const [wasteQuantity, setWasteQuantity] = useState('');
  const [wasteReason, setWasteReason] = useState('');

  const isPending = order?.status === 'Pending';
  const isInProgress = order?.status === 'InProgress';
  const isEditable = isNew || isPending;

  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const approvedRecipeOptions = (recipes?.items ?? [])
    .filter((r) => r.status === 'Approved')
    .map((r) => ({ value: r.id, label: `${r.recipeFamilyCode} v${r.versionNumber} — ${r.outputItemCode}` }));

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
    const payload = {
      warehouseId: Number(values.warehouseId),
      recipeId: Number(values.recipeId),
      plannedQuantity: Number(values.plannedQuantity),
      startDate: values.startDate || undefined
    };

    try {
      if (isNew) {
        const { id: newId } = await createMutation.mutateAsync(payload);
        showToast(t('productionOrders.createSuccess'), 'success');
        navigate(`/inventory/production-orders/${newId}`);
      } else if (order) {
        await updateMutation.mutateAsync({ ...payload, rowVersion: order.rowVersion });
        showToast(t('productionOrders.updateSuccess'), 'success');
      }
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  });

  const onComplete = async () => {
    try {
      const result = await completeMutation.mutateAsync({
        actualQuantity: Number(actualQuantity),
        endDate,
        actualWasteQuantity: wasteQuantity ? Number(wasteQuantity) : undefined,
        wasteReason: wasteReason || undefined
      });
      showToast(t('productionOrders.completeSuccessDetail', { cost: result.actualCost.toFixed(2), variance: result.costVariance.toFixed(2) }), 'success');
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
        <h2 style={{ margin: 0 }}>{isNew ? t('productionOrders.addProductionOrder') : order?.orderNumber}</h2>
        {order && <StatusBadge status={order.status} />}
      </div>

      <ActionBar
        primary={isEditable ? { key: 'save', label: isNew ? t('common.saveDraft') : t('common.saveChanges'), onClick: () => onSave() } : undefined}
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/production-orders') },
          ...(isPending ? [{ key: 'start', buttonCode: 'Start', label: t('productionOrders.start'), onClick: () => runAction(() => startMutation.mutateAsync(), t('productionOrders.startSuccess')) }] : [])
        ]}
        destructive={
          isPending || isInProgress
            ? [{ key: 'cancel', buttonCode: 'Cancel', label: t('productionOrders.cancelOrder'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('productionOrders.cancelSuccess')), confirmMessage: t('productionOrders.cancelConfirm') }]
            : []
        }
      />

      <form onSubmit={onSave} style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <Card style={{ minWidth: 0 }}>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={label('warehouse', t('productionOrders.warehouse'))}>
                <Controller
                  control={control}
                  name="warehouseId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 200 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={warehouseOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('recipe', t('productionOrders.recipe'))}>
                <Controller
                  control={control}
                  name="recipeId"
                  render={({ field }) => (
                    <SearchableSelect disabled={!isEditable} style={{ minWidth: 220 }} value={field.value} onChange={(v) => field.onChange(v === '' ? '' : Number(v))} options={approvedRecipeOptions} />
                  )}
                />
              </FieldWrapper>
              <FieldWrapper label={label('plannedQuantity', t('productionOrders.plannedQuantity'))}>
                <Input type="number" step="0.0001" disabled={!isEditable} style={{ width: 120 }} {...register('plannedQuantity', { valueAsNumber: true })} />
              </FieldWrapper>
              <FieldWrapper label={label('startDate', t('productionOrders.startDate'))}>
                <Input type="date" disabled={!isEditable} {...register('startDate')} />
              </FieldWrapper>
            </div>

            {order && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, fontSize: 13, fontWeight: 600 }}>
                <span>{t('productionOrders.standardCost')}: {order.standardCost.toFixed(2)}</span>
                {order.actualCost != null && <span>{t('productionOrders.actualCost')}: {order.actualCost.toFixed(2)}</span>}
              </div>
            )}

            {order && order.componentsPreview.length > 0 && (
              <div style={{ overflowX: 'auto', marginTop: 16 }}>
                <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
                  <thead>
                    <tr>
                      <th style={{ textAlign: 'start', padding: 8 }}>{t('productionOrders.component')}</th>
                      <th style={{ textAlign: 'start', padding: 8 }}>{t('productionOrders.plannedConsumption')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {order.componentsPreview.map((c) => (
                      <tr key={c.componentItemId}>
                        <td style={{ padding: 8 }}>{c.componentItemCode} — {c.componentItemNameAr}</td>
                        <td style={{ padding: 8 }}>{c.plannedConsumption}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardBody>
        </Card>
      </form>

      {isInProgress && (
        <Card>
          <CardBody>
            <h3 style={{ marginTop: 0 }}>{t('productionOrders.completeOrder')}</h3>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <FieldWrapper label={t('productionOrders.actualQuantity')}>
                <Input type="number" step="0.0001" style={{ width: 120 }} value={actualQuantity} onChange={(e) => setActualQuantity(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('productionOrders.endDate')}>
                <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('productionOrders.actualWasteQuantity')}>
                <Input type="number" step="0.0001" style={{ width: 120 }} value={wasteQuantity} onChange={(e) => setWasteQuantity(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('productionOrders.wasteReason')}>
                <Input value={wasteReason} onChange={(e) => setWasteReason(e.target.value)} />
              </FieldWrapper>
              <Button variant="primary" onClick={onComplete} disabled={!actualQuantity || !canComplete}>{t('productionOrders.complete')}</Button>
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
