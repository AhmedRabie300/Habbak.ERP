import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../../ui-kit/Button';
import { Input } from '../../../ui-kit/Field';
import { Card, CardBody } from '../../../ui-kit/Card';
import { StatusBadge } from '../../../ui-kit/Badge';
import { ExportMenu } from '../../../ui-kit/ExportMenu';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useItemsList } from '../items/api';
import { useWarehousesList } from '../warehouses/api';
import type { ExportColumn } from '../../../lib/export';
import {
  useBelowMinimumItemsReport, useDailyMovementReport, useInventoryCountsReport, useItemMovementReport,
  useRawMaterialConsumptionReport, useStockReport, useStockTransfersReport, useWasteReport
} from './api';
import type {
  BelowMinimumItemRow, DailyMovementRow, InventoryCountReportRow, ItemMovementRow, RawMaterialConsumptionRow,
  StockReportRow, StockTransferRow, WasteReportRow
} from './types';
import { todayLocal } from '../../../lib/date';

type ReportKey =
  | 'itemMovement' | 'stockReport' | 'belowMinimumItems' | 'inventoryCounts' | 'dailyMovement'
  | 'rawMaterialConsumption' | 'waste' | 'stockTransfers';

const REPORTS: ReportKey[] = [
  'itemMovement', 'stockReport', 'belowMinimumItems', 'inventoryCounts', 'dailyMovement',
  'rawMaterialConsumption', 'waste', 'stockTransfers'
];

const today = todayLocal();
const yearStart = `${today.slice(0, 4)}-01-01`;

/** /inventory/reports — My Remarks/Remarks2.md bug 1.3 ("لا يحتوي على أي تقارير حالياً") and
 * remark 3.8's 8-report list. Mirrors the /purchasing/reports hub pattern exactly. */
export function InventoryReportsPage() {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<ReportKey | undefined>();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('inventoryReports.title')}</h2>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ width: 280, height: 'fit-content' }}>
          <CardBody style={{ padding: 12 }}>
            {REPORTS.map((key) => (
              <ReportListItem key={key} label={t(`inventoryReports.${key}`)} active={selected === key} onClick={() => setSelected(key)} />
            ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1, minWidth: 0 }}>
          <CardBody style={{ minHeight: 300 }}>
            {!selected && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('inventoryReports.selectReport')}</p>}
            {selected === 'itemMovement' && <ItemMovementReport />}
            {selected === 'stockReport' && <StockReportView />}
            {selected === 'belowMinimumItems' && <BelowMinimumItemsReport />}
            {selected === 'inventoryCounts' && <InventoryCountsReport />}
            {selected === 'dailyMovement' && <DailyMovementReport />}
            {selected === 'rawMaterialConsumption' && <RawMaterialConsumptionReport />}
            {selected === 'waste' && <WasteReport />}
            {selected === 'stockTransfers' && <StockTransfersReport />}
          </CardBody>
        </Card>
      </div>
    </div>
  );
}

function ReportListItem({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <div
      onClick={onClick}
      style={{
        padding: '8px 10px', borderRadius: 6, cursor: 'pointer', fontSize: 13, marginBottom: 2,
        background: active ? 'var(--color-navy-500)' : 'transparent', color: active ? '#fff' : 'inherit'
      }}
    >
      {label}
    </div>
  );
}

function ReportHeader({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 16 }}>
      <h3 style={{ marginTop: 0, fontSize: 15 }}>{title}</h3>
      {children && <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap' }}>{children}</div>}
    </div>
  );
}

function FieldInline({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: 12, color: 'var(--color-text-muted)' }}>
      {label}
      {children}
    </label>
  );
}

function StaticReport<TRow>({
  title, useReportHook, columns, renderTable
}: {
  title: string;
  useReportHook: () => { data: TRow[] | undefined; isFetching: boolean };
  columns: ExportColumn<TRow>[];
  renderTable: (data: TRow[]) => React.ReactNode;
}) {
  const { t } = useTranslation();
  const { data, isFetching } = useReportHook();

  return (
    <div>
      <ReportHeader title={title}>
        {data && <ExportMenu rows={data} columns={columns} fileName={title} title={title} />}
      </ReportHeader>
      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && <div style={{ overflowX: 'auto' }}>{renderTable(data)}</div>}
    </div>
  );
}

function ItemMovementReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const { data: items } = useItemsList();
  const { data: warehouses } = useWarehousesList();
  const [itemId, setItemId] = useState(0);
  const [warehouseId, setWarehouseId] = useState(0);
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useItemMovementReport(itemId || undefined, warehouseId || undefined, from, to, run);

  const itemOptions = [{ value: 0, label: t('inventoryReports.selectItem') }, ...(items ?? []).map((i) => ({ value: i.id, label: `${i.code} — ${i.nameAr}` }))];
  const warehouseOptions = [{ value: 0, label: t('inventoryReports.allWarehouses') }, ...(warehouses ?? []).map((w) => ({ value: w.id, label: w.nameAr }))];

  const columns: ExportColumn<ItemMovementRow>[] = [
    { header: label('transactionDate', t('inventoryReports.transactionDate')), value: (r) => r.transactionDate },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('transactionType', t('inventoryReports.transactionType')), value: (r) => r.transactionType },
    { header: label('quantity', t('inventoryReports.quantity')), value: (r) => r.quantity },
    { header: label('unitCost', t('inventoryReports.unitCost')), value: (r) => r.unitCost },
    { header: label('batchNumber', t('inventoryReports.batchNumber')), value: (r) => r.batchNumber ?? '' }
  ];

  return (
    <div>
      <ReportHeader title={t('inventoryReports.itemMovement')}>
        <FieldInline label={label('item', t('inventoryReports.item'))}>
          <SearchableSelect value={itemId} onChange={(v) => setItemId(Number(v))} options={itemOptions} style={{ width: 240 }} />
        </FieldInline>
        <FieldInline label={label('warehouse', t('inventoryReports.warehouse'))}>
          <SearchableSelect value={warehouseId} onChange={(v) => setWarehouseId(Number(v))} options={warehouseOptions} style={{ width: 200 }} />
        </FieldInline>
        <FieldInline label={label('from', t('inventoryReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('inventoryReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" disabled={!itemId} onClick={() => setRun(true)}>{t('inventoryReports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('inventoryReports.itemMovement')} title={t('inventoryReports.itemMovement')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('transactionDate', t('inventoryReports.transactionDate'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('transactionType', t('inventoryReports.transactionType'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('quantity', t('inventoryReports.quantity'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('unitCost', t('inventoryReports.unitCost'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('batchNumber', t('inventoryReports.batchNumber'))}</th>
            </tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td style={{ padding: 6 }}>{r.transactionDate}</td>
                  <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                  <td style={{ padding: 6, color: r.isInbound ? 'var(--color-success)' : 'var(--color-error)', fontWeight: 600 }}>
                    {r.isInbound ? '↓ ' : '↑ '}{r.transactionType}
                  </td>
                  <td style={{ padding: 6 }}>{r.quantity.toFixed(2)}</td>
                  <td style={{ padding: 6 }}>{r.unitCost.toFixed(2)}</td>
                  <td style={{ padding: 6 }}>{r.batchNumber ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function StockReportView() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const columns: ExportColumn<StockReportRow>[] = [
    { header: label('item', t('inventoryReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('quantityOnHand', t('inventoryReports.quantityOnHand')), value: (r) => r.quantityOnHand },
    { header: label('estimatedValue', t('inventoryReports.estimatedValue')), value: (r) => r.estimatedValue }
  ];
  return (
    <StaticReport
      title={t('inventoryReports.stockReport')}
      useReportHook={useStockReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('inventoryReports.item'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('quantityOnHand', t('inventoryReports.quantityOnHand'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('estimatedValue', t('inventoryReports.estimatedValue'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={`${r.itemId}-${r.warehouseId}`}>
                <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                <td style={{ padding: 6 }}>{r.quantityOnHand.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.estimatedValue.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function BelowMinimumItemsReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const columns: ExportColumn<BelowMinimumItemRow>[] = [
    { header: label('item', t('inventoryReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('quantityOnHand', t('inventoryReports.quantityOnHand')), value: (r) => r.quantityOnHand },
    { header: label('minStockLevel', t('inventoryReports.minStockLevel')), value: (r) => r.minStockLevel },
    { header: label('shortageQuantity', t('inventoryReports.shortageQuantity')), value: (r) => r.shortageQuantity }
  ];
  return (
    <StaticReport
      title={t('inventoryReports.belowMinimumItems')}
      useReportHook={useBelowMinimumItemsReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('inventoryReports.item'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('quantityOnHand', t('inventoryReports.quantityOnHand'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('minStockLevel', t('inventoryReports.minStockLevel'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('shortageQuantity', t('inventoryReports.shortageQuantity'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={`${r.itemId}-${r.warehouseId}`}>
                <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                <td style={{ padding: 6 }}>{r.quantityOnHand.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.minStockLevel.toFixed(2)}</td>
                <td style={{ padding: 6, color: 'var(--color-error)', fontWeight: 700 }}>{r.shortageQuantity.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function InventoryCountsReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const columns: ExportColumn<InventoryCountReportRow>[] = [
    { header: label('countNumber', t('inventoryReports.countNumber')), value: (r) => r.countNumber },
    { header: label('countDate', t('inventoryReports.countDate')), value: (r) => r.countDate },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('lineCount', t('inventoryReports.lineCount')), value: (r) => r.lineCount },
    { header: label('varianceLineCount', t('inventoryReports.varianceLineCount')), value: (r) => r.varianceLineCount },
    { header: label('netVarianceQuantity', t('inventoryReports.netVarianceQuantity')), value: (r) => r.netVarianceQuantity }
  ];
  return (
    <StaticReport
      title={t('inventoryReports.inventoryCounts')}
      useReportHook={useInventoryCountsReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('countNumber', t('inventoryReports.countNumber'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('countDate', t('inventoryReports.countDate'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('status', t('inventoryReports.status'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('lineCount', t('inventoryReports.lineCount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('varianceLineCount', t('inventoryReports.varianceLineCount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('netVarianceQuantity', t('inventoryReports.netVarianceQuantity'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 6 }}>{r.countNumber}</td>
                <td style={{ padding: 6 }}>{r.countDate}</td>
                <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                <td style={{ padding: 6 }}><StatusBadge status={r.status} /></td>
                <td style={{ padding: 6 }}>{r.lineCount}</td>
                <td style={{ padding: 6, color: r.varianceLineCount > 0 ? 'var(--color-warning)' : undefined }}>{r.varianceLineCount}</td>
                <td style={{ padding: 6 }}>{r.netVarianceQuantity.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function DailyMovementReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const [date, setDate] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useDailyMovementReport(date, run);
  const columns: ExportColumn<DailyMovementRow>[] = [
    { header: label('item', t('inventoryReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('transactionType', t('inventoryReports.transactionType')), value: (r) => r.transactionType },
    { header: label('quantity', t('inventoryReports.quantity')), value: (r) => r.quantity },
    { header: label('unitCost', t('inventoryReports.unitCost')), value: (r) => r.unitCost }
  ];

  return (
    <div>
      <ReportHeader title={t('inventoryReports.dailyMovement')}>
        <FieldInline label={label('date', t('inventoryReports.date'))}><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('inventoryReports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('inventoryReports.dailyMovement')} title={t('inventoryReports.dailyMovement')} />}
      </ReportHeader>
      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('inventoryReports.item'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('transactionType', t('inventoryReports.transactionType'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('quantity', t('inventoryReports.quantity'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('unitCost', t('inventoryReports.unitCost'))}</th>
            </tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                  <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                  <td style={{ padding: 6, color: r.isInbound ? 'var(--color-success)' : 'var(--color-error)', fontWeight: 600 }}>
                    {r.isInbound ? '↓ ' : '↑ '}{r.transactionType}
                  </td>
                  <td style={{ padding: 6 }}>{r.quantity.toFixed(2)}</td>
                  <td style={{ padding: 6 }}>{r.unitCost.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function RawMaterialConsumptionReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useRawMaterialConsumptionReport(from, to, run);
  const columns: ExportColumn<RawMaterialConsumptionRow>[] = [
    { header: label('item', t('inventoryReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('totalQuantityConsumed', t('inventoryReports.totalQuantityConsumed')), value: (r) => r.totalQuantityConsumed },
    { header: label('totalValue', t('inventoryReports.totalValue')), value: (r) => r.totalValue }
  ];

  return (
    <div>
      <ReportHeader title={t('inventoryReports.rawMaterialConsumption')}>
        <FieldInline label={label('from', t('inventoryReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('inventoryReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('inventoryReports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('inventoryReports.rawMaterialConsumption')} title={t('inventoryReports.rawMaterialConsumption')} />}
      </ReportHeader>
      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('inventoryReports.item'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('totalQuantityConsumed', t('inventoryReports.totalQuantityConsumed'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('totalValue', t('inventoryReports.totalValue'))}</th>
            </tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.itemId}>
                  <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                  <td style={{ padding: 6 }}>{r.totalQuantityConsumed.toFixed(2)}</td>
                  <td style={{ padding: 6 }}>{r.totalValue.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function WasteReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const columns: ExportColumn<WasteReportRow>[] = [
    { header: label('wasteDate', t('inventoryReports.wasteDate')), value: (r) => r.wasteDate },
    { header: label('item', t('inventoryReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('warehouse', t('inventoryReports.warehouse')), value: (r) => r.warehouseNameAr },
    { header: label('quantity', t('inventoryReports.quantity')), value: (r) => r.quantity },
    { header: label('estimatedValue', t('inventoryReports.estimatedValue')), value: (r) => r.estimatedValue },
    { header: label('reason', t('inventoryReports.reason')), value: (r) => r.reason }
  ];
  return (
    <StaticReport
      title={t('inventoryReports.waste')}
      useReportHook={useWasteReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('wasteDate', t('inventoryReports.wasteDate'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('inventoryReports.item'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('warehouse', t('inventoryReports.warehouse'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('quantity', t('inventoryReports.quantity'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('estimatedValue', t('inventoryReports.estimatedValue'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('reason', t('inventoryReports.reason'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 6 }}>{r.wasteDate}</td>
                <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                <td style={{ padding: 6 }}>{r.warehouseNameAr}</td>
                <td style={{ padding: 6 }}>{r.quantity.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.estimatedValue.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.reason}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function StockTransfersReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('INVENTORY_REPORTS');
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useStockTransfersReport(from, to, run);
  const columns: ExportColumn<StockTransferRow>[] = [
    { header: label('documentNumber', t('inventoryReports.documentNumber')), value: (r) => r.documentNumber },
    { header: label('documentDate', t('inventoryReports.documentDate')), value: (r) => r.documentDate },
    { header: label('documentType', t('inventoryReports.documentType')), value: (r) => r.documentType },
    { header: label('sourceWarehouse', t('inventoryReports.sourceWarehouse')), value: (r) => r.sourceWarehouseNameAr ?? '' },
    { header: label('destinationWarehouse', t('inventoryReports.destinationWarehouse')), value: (r) => r.destinationWarehouseNameAr ?? '' },
    { header: label('totalQuantity', t('inventoryReports.totalQuantity')), value: (r) => r.totalQuantity }
  ];

  return (
    <div>
      <ReportHeader title={t('inventoryReports.stockTransfers')}>
        <FieldInline label={label('from', t('inventoryReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('inventoryReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('inventoryReports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('inventoryReports.stockTransfers')} title={t('inventoryReports.stockTransfers')} />}
      </ReportHeader>
      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('documentNumber', t('inventoryReports.documentNumber'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('documentDate', t('inventoryReports.documentDate'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('documentType', t('inventoryReports.documentType'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('sourceWarehouse', t('inventoryReports.sourceWarehouse'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('destinationWarehouse', t('inventoryReports.destinationWarehouse'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('status', t('inventoryReports.status'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('totalQuantity', t('inventoryReports.totalQuantity'))}</th>
            </tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td style={{ padding: 6 }}>{r.documentNumber}</td>
                  <td style={{ padding: 6 }}>{r.documentDate}</td>
                  <td style={{ padding: 6 }}>{r.documentType}</td>
                  <td style={{ padding: 6 }}>{r.sourceWarehouseNameAr ?? '—'}</td>
                  <td style={{ padding: 6 }}>{r.destinationWarehouseNameAr ?? '—'}</td>
                  <td style={{ padding: 6 }}><StatusBadge status={r.status} /></td>
                  <td style={{ padding: 6 }}>{r.totalQuantity.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
