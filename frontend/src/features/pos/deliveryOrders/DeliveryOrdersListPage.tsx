import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Card, CardBody } from '../../../ui-kit/Card';
import { FieldWrapper, Input } from '../../../ui-kit/Field';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useToastStore } from '../../../store/toastStore';
import { getFieldErrorMessage } from '../../../app/api';
import { usePOSTerminalsList } from '../terminals/api';
import { useItemsList } from '../../inventory/items/api';
import { useDeliveryOrdersList, useReceiveDeliveryOrder } from './api';
import type { DeliveryOrderItemInput, DeliveryOrderListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';

/** The grid keys its rows by id; a delivery order is identified by its check. */
type DeliveryOrderRow = DeliveryOrderListItem & { id: number };

interface DraftLine {
  itemId: number | '';
  quantity: number;
  unitPrice: number | '';
}

/** /pos/delivery-orders — screen #10 (05-Module-POS-Shifts.md، قاعدة 17): يجمع طلبات المنصات
 * الخارجية مع الدليفري الداخلي في قائمة واحدة. مفيش تكامل API فعلي مع Talabat/Elmenus (يحتاج
 * مفاتيح/عقود تجارية غير متاحة) — زر "استقبال طلب" هنا بيمثّل الـwebhook اللي منصة حقيقية هتنادي
 * عليه، ومُستخدَم كمحاكاة/إدخال يدوي من الكاشير لحد ما التكامل الفعلي يتوفر. */
export function DeliveryOrdersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [search, setSearch] = useState('');
  const { data: orders, isLoading } = useDeliveryOrdersList();
  const { data: terminals } = usePOSTerminalsList();
  const { data: items } = useItemsList();

  const [showReceiveModal, setShowReceiveModal] = useState(false);
  const [posTerminalId, setPosTerminalId] = useState<number | ''>('');
  const [platformName, setPlatformName] = useState('Talabat');
  const [platformOrderId, setPlatformOrderId] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [deliveryAddress, setDeliveryAddress] = useState('');
  const [lines, setLines] = useState<DraftLine[]>([{ itemId: '', quantity: 1, unitPrice: '' }]);

  const receiveMutation = useReceiveDeliveryOrder();

  const filtered = (orders ?? []).filter((o) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return (
      o.checkCode.toLowerCase().includes(query) ||
      (o.platformOrderId ?? '').toLowerCase().includes(query) ||
      (o.customerName ?? '').toLowerCase().includes(query)
    );
  });

  // The grid keys its rows by id; a delivery order is identified by its check.
  const data: PagedResult<DeliveryOrderRow> = {
    items: filtered.map((o) => ({ ...o, id: o.checkId })), totalCount: filtered.length, page: 1, pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<DeliveryOrderRow>[] = [
    { key: 'checkCode', label: t('deliveryOrders.checkCode'), render: (o) => o.checkCode, exportValue: (o) => o.checkCode },
    {
      key: 'source', label: t('deliveryOrders.source'),
      render: (o) => (o.isExternalPlatform ? `${o.platformName} — ${o.platformOrderId}` : t('deliveryOrders.internal')),
      exportValue: (o) => (o.isExternalPlatform ? `${o.platformName} — ${o.platformOrderId}` : t('deliveryOrders.internal'))
    },
    { key: 'customerName', label: t('deliveryOrders.customer'), render: (o) => o.customerName ?? '—', exportValue: (o) => o.customerName ?? '' },
    { key: 'lineCount', label: t('deliveryOrders.lineCount'), render: (o) => o.lineCount, exportValue: (o) => o.lineCount },
    { key: 'total', label: t('deliveryOrders.total'), render: (o) => o.total.toFixed(2), exportValue: (o) => o.total },
    { key: 'status', label: t('deliveryOrders.status'), render: (o) => <StatusBadge status={o.status} />, exportValue: (o) => o.status }
  ];

  const terminalOptions = (terminals ?? []).map((tItem) => ({ value: tItem.id, label: `${tItem.code} — ${tItem.nameAr}` }));
  const itemOptions = (items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }));

  const updateLine = (index: number, patch: Partial<DraftLine>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  };

  const addLine = () => setLines((prev) => [...prev, { itemId: '', quantity: 1, unitPrice: '' }]);
  const removeLine = (index: number) => setLines((prev) => prev.filter((_, i) => i !== index));

  const resetForm = () => {
    setPosTerminalId('');
    setPlatformName('Talabat');
    setPlatformOrderId('');
    setCustomerName('');
    setCustomerPhone('');
    setDeliveryAddress('');
    setLines([{ itemId: '', quantity: 1, unitPrice: '' }]);
  };

  const handleReceive = async () => {
    const validLines = lines.filter((l) => l.itemId !== '' && l.quantity > 0);
    if (posTerminalId === '' || !platformOrderId.trim() || validLines.length === 0) {
      showToast(t('deliveryOrders.invalidForm'), 'error');
      return;
    }
    const payloadItems: DeliveryOrderItemInput[] = validLines.map((l) => ({
      itemId: l.itemId as number,
      quantity: l.quantity,
      unitPrice: l.unitPrice === '' ? undefined : l.unitPrice
    }));
    try {
      await receiveMutation.mutateAsync({
        posTerminalId, platformName: platformName.trim(), platformOrderId: platformOrderId.trim(),
        customerName: customerName.trim() || undefined, customerPhone: customerPhone.trim() || undefined,
        deliveryAddress: deliveryAddress.trim() || undefined, items: payloadItems
      });
      showToast(t('deliveryOrders.receiveSuccess'), 'success');
      setShowReceiveModal(false);
      resetForm();
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('deliveryOrders.title')}</h2>
        <Button variant="primary" onClick={() => setShowReceiveModal(true)}>{t('deliveryOrders.receiveOrder')}</Button>
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/checks/${row.checkId}`)}
        exportFileName={t('deliveryOrders.title')}
      />

      {showReceiveModal && (
        <div
          role="dialog" aria-modal="true"
          style={{ position: 'fixed', inset: 0, background: 'rgba(15,23,42,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}
          onClick={() => setShowReceiveModal(false)}
        >
          <div style={{ background: 'var(--color-surface)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-lg)', width: '92%', maxWidth: 640, maxHeight: '90vh', overflowY: 'auto' }} onClick={(e) => e.stopPropagation()}>
            <div style={{ padding: '18px 24px', borderBottom: '1px solid var(--color-border)', fontWeight: 700, fontSize: 16 }}>
              {t('deliveryOrders.receiveOrder')}
            </div>
            <div style={{ padding: '16px 24px' }}>
              <Card>
                <CardBody>
                  <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                    <FieldWrapper label={t('deliveryOrders.terminal')}>
                      <SearchableSelect style={{ minWidth: 200 }} value={posTerminalId} onChange={(v) => setPosTerminalId(v === '' ? '' : Number(v))} options={terminalOptions} />
                    </FieldWrapper>
                    <FieldWrapper label={t('deliveryOrders.platformName')}>
                      <Input style={{ width: 160 }} value={platformName} onChange={(e) => setPlatformName(e.target.value)} />
                    </FieldWrapper>
                    <FieldWrapper label={t('deliveryOrders.platformOrderId')}>
                      <Input style={{ width: 160 }} value={platformOrderId} onChange={(e) => setPlatformOrderId(e.target.value)} />
                    </FieldWrapper>
                    <FieldWrapper label={t('deliveryOrders.customerName')}>
                      <Input style={{ width: 180 }} value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
                    </FieldWrapper>
                    <FieldWrapper label={t('deliveryOrders.customerPhone')}>
                      <Input style={{ width: 140 }} value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} />
                    </FieldWrapper>
                    <FieldWrapper label={t('deliveryOrders.deliveryAddress')}>
                      <Input style={{ minWidth: 240 }} value={deliveryAddress} onChange={(e) => setDeliveryAddress(e.target.value)} />
                    </FieldWrapper>
                  </div>

                  <div style={{ marginTop: 16 }}>
                    <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 8 }}>{t('deliveryOrders.items')}</div>
                    {lines.map((line, index) => (
                      <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
                        <SearchableSelect style={{ minWidth: 200 }} value={line.itemId} onChange={(v) => updateLine(index, { itemId: v === '' ? '' : Number(v) })} options={itemOptions} />
                        <Input type="number" step="0.0001" min={0} style={{ width: 80 }} value={line.quantity} onChange={(e) => updateLine(index, { quantity: Number(e.target.value) })} placeholder={t('checkEdit.quantity')} />
                        <Input type="number" step="0.01" min={0} style={{ width: 100 }} value={line.unitPrice} onChange={(e) => updateLine(index, { unitPrice: e.target.value === '' ? '' : Number(e.target.value) })} placeholder={t('checkEdit.unitPrice')} />
                        <button onClick={() => removeLine(index)} title={t('common.remove')} style={{ background: 'transparent', border: 'none', color: 'var(--color-error)', fontSize: 15, cursor: 'pointer' }}>✕</button>
                      </div>
                    ))}
                    <Button variant="ghost" onClick={addLine}>{t('deliveryOrders.addItem')}</Button>
                  </div>
                </CardBody>
              </Card>
            </div>
            <div style={{ padding: '16px 24px', borderTop: '1px solid var(--color-border)', display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <Button variant="ghost" onClick={() => setShowReceiveModal(false)}>{t('common.cancel')}</Button>
              <Button variant="primary" onClick={handleReceive}>{t('deliveryOrders.save')}</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
