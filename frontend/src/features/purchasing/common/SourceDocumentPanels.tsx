import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';
import { Card, CardBody } from '../../../ui-kit/Card';

/**
 * The "where did this document come from" panels (Remarks4, items 4 and 5): a purchase order shows
 * what its purchase request asked for, and a purchase invoice shows what its order covered and how
 * much of it actually arrived. Both collapse, both stay out of the way when there is no source
 * document, and neither lets the user change anything — they exist so a buyer can compare without
 * opening a second tab.
 */
interface RequestLineCoverage {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  requestedQuantity: number;
  requestedUnitCode: string;
  requestedBaseQuantity: number;
  orderedOnThisOrderBaseQuantity: number;
  orderedElsewhereBaseQuantity: number;
  remainingBaseQuantity: number;
  notes?: string | null;
}

interface SourceRequest {
  purchaseRequestId: number;
  requestNumber: string;
  requestDate: string;
  priority: string;
  status: string;
  requestedByName?: string | null;
  reason?: string | null;
  notes?: string | null;
  lines: RequestLineCoverage[];
}

interface OrderLineFulfillment {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  orderedQuantity: number;
  orderedUnitCode: string;
  orderedBaseQuantity: number;
  receivedBaseQuantity: number;
  invoicedOnThisInvoiceBaseQuantity: number;
  invoicedElsewhereBaseQuantity: number;
  remainingBaseQuantity: number;
  orderedUnitPrice: number;
  invoicedBaseUnitCost?: number | null;
}

interface SourceOrder {
  purchaseOrderId: number;
  orderNumber: string;
  orderDate: string;
  status: string;
  currencyCode: string;
  totalAmount: number;
  expectedDeliveryDate?: string | null;
  purchaseRequestNumber?: string | null;
  lines: OrderLineFulfillment[];
}

const num = (value: number) => value.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 4 });

const cell: React.CSSProperties = { padding: '6px 8px', textAlign: 'start' };

function CollapsibleCard({ title, subtitle, children }: { title: string; subtitle: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true);
  return (
    <Card>
      <CardBody>
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          style={{
            all: 'unset', cursor: 'pointer', display: 'flex', width: '100%', justifyContent: 'space-between', alignItems: 'center', gap: 12
          }}
        >
          <span style={{ fontWeight: 600 }}>{title}</span>
          <span style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>
            {subtitle} {open ? '▾' : '▸'}
          </span>
        </button>
        {open && <div style={{ marginTop: 12, overflowX: 'auto' }}>{children}</div>}
      </CardBody>
    </Card>
  );
}

/** Item 4 — the purchase request behind an order. Renders nothing when the order has none. */
export function PurchaseOrderSourceRequestPanel({ orderId }: { orderId: number | undefined }) {
  const { t } = useTranslation();
  const { data } = useQuery({
    queryKey: ['purchase-orders', orderId, 'source-request'],
    queryFn: async () => (await api.get<SourceRequest | null>(`/purchasing/purchase-orders/${orderId}/source-request`)).data,
    enabled: orderId !== undefined
  });

  if (!data) return null;

  return (
    <CollapsibleCard
      title={t('purchaseOrders.sourceRequestTitle')}
      subtitle={`${data.requestNumber} · ${data.requestDate} · ${data.requestedByName ?? '—'}`}
    >
      {data.reason && <p style={{ marginTop: 0, color: 'var(--color-text-muted)' }}>{data.reason}</p>}
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            <th style={cell}>{t('purchaseOrders.item')}</th>
            <th style={cell}>{t('purchaseOrders.requestedQuantity')}</th>
            <th style={cell}>{t('purchaseOrders.onThisOrder')}</th>
            <th style={cell}>{t('purchaseOrders.onOtherOrders')}</th>
            <th style={cell}>{t('purchaseOrders.notOrderedYet')}</th>
          </tr>
        </thead>
        <tbody>
          {data.lines.map((line) => (
            <tr key={line.itemId} style={{ borderTop: '1px solid var(--color-border)' }}>
              <td style={cell}>{`${line.itemCode} — ${line.itemNameAr}`}</td>
              <td style={cell}>{`${num(line.requestedQuantity)} ${line.requestedUnitCode}`}</td>
              <td style={cell}>{num(line.orderedOnThisOrderBaseQuantity)}</td>
              <td style={cell}>{num(line.orderedElsewhereBaseQuantity)}</td>
              <td style={{ ...cell, color: line.remainingBaseQuantity > 0 ? 'var(--color-warning)' : undefined }}>
                {num(line.remainingBaseQuantity)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p style={{ color: 'var(--color-text-muted)', fontSize: 12, marginBottom: 0 }}>{t('purchaseOrders.baseUnitsNote')}</p>
    </CollapsibleCard>
  );
}

/** Item 5 — the order behind an invoice, with what was received against it. */
export function PurchaseInvoiceSourceOrderPanel({ invoiceId }: { invoiceId: number | undefined }) {
  const { t } = useTranslation();
  const { data } = useQuery({
    queryKey: ['purchase-invoices', invoiceId, 'source-order'],
    queryFn: async () => (await api.get<SourceOrder | null>(`/purchasing/purchase-invoices/${invoiceId}/source-order`)).data,
    enabled: invoiceId !== undefined
  });

  if (!data) return null;

  return (
    <CollapsibleCard
      title={t('purchaseInvoices.sourceOrderTitle')}
      subtitle={`${data.orderNumber} · ${data.orderDate} · ${num(data.totalAmount)} ${data.currencyCode}`}
    >
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
        <thead>
          <tr>
            <th style={cell}>{t('purchaseInvoices.item')}</th>
            <th style={cell}>{t('purchaseInvoices.orderedQuantity')}</th>
            <th style={cell}>{t('purchaseInvoices.receivedQuantity')}</th>
            <th style={cell}>{t('purchaseInvoices.onThisInvoice')}</th>
            <th style={cell}>{t('purchaseInvoices.onOtherInvoices')}</th>
            <th style={cell}>{t('purchaseInvoices.notInvoicedYet')}</th>
            <th style={cell}>{t('purchaseInvoices.orderedPrice')}</th>
          </tr>
        </thead>
        <tbody>
          {data.lines.map((line) => {
            const priceDrift =
              line.invoicedBaseUnitCost != null && Math.abs(line.invoicedBaseUnitCost - line.orderedUnitPrice) > 0.0001;
            return (
              <tr key={line.itemId} style={{ borderTop: '1px solid var(--color-border)' }}>
                <td style={cell}>{`${line.itemCode} — ${line.itemNameAr}`}</td>
                <td style={cell}>{`${num(line.orderedQuantity)} ${line.orderedUnitCode}`}</td>
                <td style={{ ...cell, color: line.receivedBaseQuantity < line.invoicedOnThisInvoiceBaseQuantity ? 'var(--color-error)' : undefined }}>
                  {num(line.receivedBaseQuantity)}
                </td>
                <td style={cell}>{num(line.invoicedOnThisInvoiceBaseQuantity)}</td>
                <td style={cell}>{num(line.invoicedElsewhereBaseQuantity)}</td>
                <td style={cell}>{num(line.remainingBaseQuantity)}</td>
                <td style={{ ...cell, color: priceDrift ? 'var(--color-warning)' : undefined }}>
                  {num(line.orderedUnitPrice)}
                  {priceDrift && ` → ${num(line.invoicedBaseUnitCost!)}`}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p style={{ color: 'var(--color-text-muted)', fontSize: 12, marginBottom: 0 }}>{t('purchaseInvoices.receivedFromReceiptsNote')}</p>
    </CollapsibleCard>
  );
}
