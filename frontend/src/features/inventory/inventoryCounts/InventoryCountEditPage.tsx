import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useButtonPermission } from '../../auth/access';
import { Button } from '../../../ui-kit/Button';
import { ActionBar } from '../../../ui-kit/ActionBar';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { useWarehousesList } from '../warehouses/api';
import { useItemsList } from '../items/api';
import { UnitSelect } from '../items/UnitSelect';
import {
  useCancelInventoryCount, useCloseInventoryCount, useCompleteInventoryCountSettlement, useCreateInventoryCount,
  useInventoryCount, useRecordCountedQuantities, useRejectInventoryCount, useSettleInventoryCountLine,
  useStartInventoryCount, useSubmitInventoryCountForSettlement
} from './api';
import { todayLocal } from '../../../lib/date';

const COUNT_TYPES = ['Full', 'Partial', 'Surprise', 'Cyclic'] as const;

/** /inventory/inventory-counts/:id — screen #14's full multi-stage cycle in one page, switching
 * layout by Status rather than routing across separate pages per stage: Create (Draft, before any
 * count exists) → Counters (InProgress) → interactive settlement (PendingSettlement) → Close
 * (Settled). Each stage only exposes the actions valid for it (section 4.3's state machine). */
export function InventoryCountEditPage() {
  const { t } = useTranslation();
  const canSettle = useButtonPermission('INVENTORY_COUNTS', 'SettleLine');
  const { id } = useParams();
  const isNew = id === 'new';
  const countId = isNew ? undefined : Number(id);
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);

  const { data: count, isLoading } = useInventoryCount(countId);
  const { data: warehouses } = useWarehousesList();
  const { data: items } = useItemsList();

  const [warehouseId, setWarehouseId] = useState<number | ''>('');
  const [countDate, setCountDate] = useState(todayLocal());
  const [countType, setCountType] = useState<(typeof COUNT_TYPES)[number]>('Full');
  const [selectedItemIds, setSelectedItemIds] = useState<number[]>([]);
  const [itemToAdd, setItemToAdd] = useState<number | ''>('');

  const [countedQuantities, setCountedQuantities] = useState<Record<number, string>>({});
  // The unit each line is counted in (Remarks3 item 16) — starts at the item's base unit.
  const [countedUnits, setCountedUnits] = useState<Record<number, number>>({});
  const [settlements, setSettlements] = useState<Record<number, { decision: 'Approved' | 'Rejected' | ''; reason: string }>>({});

  useEffect(() => {
    if (count) {
      setCountedQuantities(Object.fromEntries(count.lines.map((l) => [l.id, l.countedQuantity?.toString() ?? ''])));
      setCountedUnits(Object.fromEntries(count.lines.map((l) => [l.id, l.unitId])));
      setSettlements(Object.fromEntries(count.lines.map((l) => [
        l.id,
        { decision: (l.settlementDecision === 'Pending' ? '' : l.settlementDecision) as 'Approved' | 'Rejected' | '', reason: l.settlementReason ?? '' }
      ])));
    }
  }, [count]);

  const createMutation = useCreateInventoryCount();
  const startMutation = useStartInventoryCount(countId ?? 0);
  const recordMutation = useRecordCountedQuantities(countId ?? 0);
  const submitForSettlementMutation = useSubmitInventoryCountForSettlement(countId ?? 0);
  const settleLineMutation = useSettleInventoryCountLine(countId ?? 0);
  const completeSettlementMutation = useCompleteInventoryCountSettlement(countId ?? 0);
  const closeMutation = useCloseInventoryCount(countId ?? 0);
  const rejectMutation = useRejectInventoryCount(countId ?? 0);
  const cancelMutation = useCancelInventoryCount(countId ?? 0);

  const warehouseOptions = (warehouses ?? []).map((w) => ({ value: w.id, label: `${w.code} — ${w.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));
  const needsManualItems = countType === 'Partial' || countType === 'Cyclic';

  const runAction = async (action: () => Promise<unknown>, successMessage: string) => {
    try {
      await action();
      showToast(successMessage, 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const handleCreate = () => runAction(async () => {
    const { id: newId } = await createMutation.mutateAsync({
      warehouseId: Number(warehouseId),
      countDate,
      countType,
      itemIds: needsManualItems ? selectedItemIds : undefined
    });
    navigate(`/inventory/inventory-counts/${newId}`);
  }, t('inventoryCounts.createSuccess'));

  const handleSaveCounts = () => runAction(() => recordMutation.mutateAsync(
    Object.entries(countedQuantities)
      .filter(([, v]) => v !== '')
      .map(([lineId, v]) => ({ lineId: Number(lineId), countedQuantity: Number(v), unitId: countedUnits[Number(lineId)] }))
  ), t('inventoryCounts.countsSaved'));

  const handleSettleLine = (lineId: number) => {
    const s = settlements[lineId];
    if (!s?.decision) return;
    runAction(() => settleLineMutation.mutateAsync({ lineId, input: { decision: s.decision as 'Approved' | 'Rejected', settlementReason: s.reason || undefined } }), t('inventoryCounts.lineSettled'));
  };

  const handleClose = async () => {
    try {
      const result = await closeMutation.mutateAsync();
      showToast(t('inventoryCounts.closeSuccessDetail', { status: t(`status.${result.status}`) }), 'success');
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  if (!isNew && isLoading) {
    return <div>{t('common.loading')}</div>;
  }

  if (isNew) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
        <h2 style={{ margin: 0 }}>{t('inventoryCounts.addInventoryCount')}</h2>
        <ActionBar
          primary={{ key: 'save', label: t('common.saveDraft'), onClick: handleCreate }}
          secondary={[{ key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/inventory-counts') }]}
        />
        <Card>
          <CardBody>
            <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
              <FieldWrapper label={t('inventoryCounts.warehouse')}>
                <SearchableSelect style={{ minWidth: 200 }} value={warehouseId} onChange={(v) => setWarehouseId(v === '' ? '' : Number(v))} options={warehouseOptions} />
              </FieldWrapper>
              <FieldWrapper label={t('inventoryCounts.countDate')}>
                <Input type="date" value={countDate} onChange={(e) => setCountDate(e.target.value)} />
              </FieldWrapper>
              <FieldWrapper label={t('inventoryCounts.countType')}>
                <SearchableSelect
                  style={{ minWidth: 160 }}
                  value={countType}
                  onChange={(v) => setCountType(v as (typeof COUNT_TYPES)[number])}
                  options={COUNT_TYPES.map((ct) => ({ value: ct, label: t(`inventoryCounts.type${ct}`) }))}
                />
              </FieldWrapper>
            </div>

            {needsManualItems && (
              <div style={{ marginTop: 16 }}>
                <FieldWrapper label={t('inventoryCounts.addItemToCount')}>
                  <div style={{ display: 'flex', gap: 8 }}>
                    <SearchableSelect
                      style={{ minWidth: 220 }}
                      value={itemToAdd}
                      onChange={(v) => setItemToAdd(v === '' ? '' : Number(v))}
                      options={itemOptions.filter((o) => !selectedItemIds.includes(Number(o.value)))}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => {
                        if (itemToAdd !== '') {
                          setSelectedItemIds((prev) => [...prev, Number(itemToAdd)]);
                          setItemToAdd('');
                        }
                      }}
                    >
                      {t('inventoryCounts.addItem')}
                    </Button>
                  </div>
                </FieldWrapper>
                <ul style={{ marginTop: 8 }}>
                  {selectedItemIds.map((itemId) => {
                    const opt = itemOptions.find((o) => o.value === itemId);
                    return (
                      <li key={itemId} style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13 }}>
                        {opt?.label ?? itemId}
                        <Button type="button" variant="ghost" onClick={() => setSelectedItemIds((prev) => prev.filter((i) => i !== itemId))}>
                          {t('common.remove')}
                        </Button>
                      </li>
                    );
                  })}
                </ul>
              </div>
            )}
          </CardBody>
        </Card>
      </div>
    );
  }

  if (!count) {
    return null;
  }

  const isDraft = count.status === 'Draft';
  const isInProgress = count.status === 'InProgress';
  const isPendingSettlement = count.status === 'PendingSettlement';
  const isSettled = count.status === 'Settled';
  const allLinesCounted = count.lines.every((l) => countedQuantities[l.id] !== '' && countedQuantities[l.id] !== undefined);
  const allLinesSettled = count.lines.every((l) => settlements[l.id]?.decision);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16, minWidth: 0 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{count.countNumber} — {count.warehouseCode}</h2>
        <StatusBadge status={count.status} />
      </div>

      <ActionBar
        primary={
          isDraft ? { key: 'start', buttonCode: 'Start', label: t('inventoryCounts.start'), onClick: () => runAction(() => startMutation.mutateAsync(), t('inventoryCounts.startSuccess')) }
          : isInProgress ? { key: 'submit', buttonCode: 'SubmitForSettlement', label: t('inventoryCounts.submitForSettlement'), onClick: () => runAction(() => submitForSettlementMutation.mutateAsync(), t('inventoryCounts.submitSuccess')), disabled: !allLinesCounted }
          : isPendingSettlement ? { key: 'complete', buttonCode: 'CompleteSettlement', label: t('inventoryCounts.completeSettlement'), onClick: () => runAction(() => completeSettlementMutation.mutateAsync(), t('inventoryCounts.settlementCompleteSuccess')), disabled: !allLinesSettled }
          : isSettled ? { key: 'close', buttonCode: 'Close', label: t('inventoryCounts.close'), onClick: handleClose }
          : undefined
        }
        secondary={[
          { key: 'back', label: t('common.back'), onClick: () => navigate('/inventory/inventory-counts') },
          ...(isInProgress ? [{ key: 'saveCounts', label: t('inventoryCounts.saveCounts'), onClick: handleSaveCounts }] : [])
        ]}
        destructive={[
          ...(isDraft || isInProgress ? [{ key: 'cancel', buttonCode: 'Cancel', label: t('inventoryCounts.cancelCount'), onClick: () => runAction(() => cancelMutation.mutateAsync(), t('inventoryCounts.cancelSuccess')), confirmMessage: t('inventoryCounts.cancelConfirm') }] : []),
          ...(isSettled ? [{ key: 'reject', buttonCode: 'Reject', label: t('inventoryCounts.reject'), onClick: () => runAction(() => rejectMutation.mutateAsync(), t('inventoryCounts.rejectSuccess')), confirmMessage: t('inventoryCounts.rejectConfirm') }] : [])
        ]}
      />

      <Card style={{ minWidth: 0 }}>
        <CardBody>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ fontSize: 13, borderCollapse: 'collapse', width: '100%' }}>
              <thead>
                <tr>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.item')}</th>
                  <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.systemQuantity')}</th>
                  {!isDraft && <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.unit')}</th>}
                  {!isDraft && <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.countedQuantity')}</th>}
                  {(isPendingSettlement || isSettled || count.status === 'Closed' || count.status === 'Rejected') && (
                    <>
                      <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.varianceQuantity')}</th>
                      <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.settlementDecision')}</th>
                      <th style={{ textAlign: 'start', padding: 8 }}>{t('inventoryCounts.settlementReason')}</th>
                    </>
                  )}
                  {isPendingSettlement && <th />}
                </tr>
              </thead>
              <tbody>
                {count.lines.map((line) => (
                  <tr key={line.id}>
                    <td style={{ padding: 8 }}>{line.itemCode} — {line.itemNameAr}</td>
                    <td style={{ padding: 8 }}>{line.systemQuantity} {line.baseUnitCode ?? ''}</td>
                    {!isDraft && (
                      <td style={{ padding: 8 }}>
                        {isInProgress ? (
                          <UnitSelect
                            item={items?.find((i) => i.id === line.itemId)}
                            value={countedUnits[line.id] ?? line.unitId}
                            onChange={(unitId) => setCountedUnits((prev) => ({ ...prev, [line.id]: unitId }))}
                          />
                        ) : (line.unitNameAr ?? '—')}
                      </td>
                    )}
                    {!isDraft && (
                      <td style={{ padding: 8 }}>
                        {isInProgress ? (
                          <Input
                            type="number"
                            step="0.0001"
                            style={{ width: 110 }}
                            value={countedQuantities[line.id] ?? ''}
                            onChange={(e) => setCountedQuantities((prev) => ({ ...prev, [line.id]: e.target.value }))}
                          />
                        ) : (line.countedQuantity ?? '—')}
                      </td>
                    )}
                    {(isPendingSettlement || isSettled || count.status === 'Closed' || count.status === 'Rejected') && (
                      <>
                        <td style={{ padding: 8 }}>{line.varianceQuantity ?? 0} {line.baseUnitCode ?? ''}</td>
                        <td style={{ padding: 8 }}>
                          {isPendingSettlement ? (
                            <SearchableSelect
                              style={{ width: 120 }}
                              value={settlements[line.id]?.decision ?? ''}
                              onChange={(v) => setSettlements((prev) => ({ ...prev, [line.id]: { ...prev[line.id], decision: v as 'Approved' | 'Rejected' } }))}
                              options={[
                                { value: 'Approved', label: t('inventoryCounts.approve') },
                                { value: 'Rejected', label: t('inventoryCounts.rejectLine') }
                              ]}
                            />
                          ) : t(`status.${line.settlementDecision}`, line.settlementDecision)}
                        </td>
                        <td style={{ padding: 8 }}>
                          {isPendingSettlement ? (
                            <Input
                              style={{ width: 180 }}
                              value={settlements[line.id]?.reason ?? ''}
                              onChange={(e) => setSettlements((prev) => ({ ...prev, [line.id]: { ...prev[line.id], reason: e.target.value } }))}
                            />
                          ) : (line.settlementReason ?? '—')}
                        </td>
                      </>
                    )}
                    {isPendingSettlement && (
                      <td style={{ padding: 8 }}>
                        <Button type="button" variant="ghost" onClick={() => handleSettleLine(line.id)} disabled={!canSettle || !settlements[line.id]?.decision}>
                          {t('inventoryCounts.settleLine')}
                        </Button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardBody>
      </Card>
    </div>
  );
}
