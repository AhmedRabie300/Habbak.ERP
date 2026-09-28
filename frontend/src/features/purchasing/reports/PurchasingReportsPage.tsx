import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../../ui-kit/Button';
import { Input } from '../../../ui-kit/Field';
import { Card, CardBody } from '../../../ui-kit/Card';
import { StatusBadge } from '../../../ui-kit/Badge';
import { ExportMenu } from '../../../ui-kit/ExportMenu';
import { useFieldLabels } from '../../common/useFieldLabels';
import type { ExportColumn } from '../../../lib/export';
import {
  useAdditionalCostAllocationReport, useExpiredQuotesReport, useExpiringContractsReport, useOpenPurchaseOrdersReport,
  usePurchaseExpensesByTypeReport, usePurchaseExpensesReport, usePurchaseReturnsReport, usePurchasesByItemReport,
  usePurchasesBySupplierReport, useSupplierEvaluationRankingReport, useUnpaidInvoicesReport
} from './api';
import type {
  AllocationByItemRow, ExpiredRFQQuoteRow, ExpiringSupplierContractRow, OpenPurchaseOrderRow, PurchaseExpenseReportRow,
  PurchaseReturnReportRow, PurchasesByItemRow, PurchasesBySupplierRow, SupplierEvaluationRankingRow, UnpaidPurchaseInvoiceRow
} from './types';
import { todayLocal } from '../../../lib/date';

type ReportKey =
  | 'purchasesBySupplier' | 'purchasesByItem' | 'openPurchaseOrders' | 'unpaidInvoices' | 'purchaseReturns'
  | 'priceHistory' | 'supplierEvaluationRanking' | 'purchaseExpenses' | 'expiringContracts' | 'additionalCostAllocation'
  | 'expiredQuotes';

const REPORTS: ReportKey[] = [
  'purchasesBySupplier', 'purchasesByItem', 'openPurchaseOrders', 'unpaidInvoices', 'purchaseReturns',
  'priceHistory', 'supplierEvaluationRanking', 'purchaseExpenses', 'expiringContracts', 'additionalCostAllocation',
  'expiredQuotes'
];

const today = todayLocal();
const yearStart = `${today.slice(0, 4)}-01-01`;

/** /purchasing/reports (03-Module-Purchasing.md, section 11). #6 "تحليل أسعار الشراء التاريخية"
 * redirects to the already-built /purchasing/supplier-price-history (screen #13) rather than
 * duplicating that report here. */
export function PurchasingReportsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [selected, setSelected] = useState<ReportKey | undefined>();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t('purchasingReports.title')}</h2>

      <div style={{ display: 'flex', gap: 24 }}>
        <Card style={{ width: 280, height: 'fit-content' }}>
          <CardBody style={{ padding: 12 }}>
            {REPORTS.map((key) => (
              <ReportListItem
                key={key}
                label={t(`purchasingReports.${key}`)}
                active={selected === key}
                onClick={() => key === 'priceHistory' ? navigate('/purchasing/supplier-price-history') : setSelected(key)}
              />
            ))}
          </CardBody>
        </Card>

        <Card style={{ flex: 1, minWidth: 0 }}>
          <CardBody style={{ minHeight: 300 }}>
            {!selected && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('purchasingReports.selectReport')}</p>}
            {selected === 'purchasesBySupplier' && <PurchasesBySupplierReport />}
            {selected === 'purchasesByItem' && <PurchasesByItemReport />}
            {selected === 'openPurchaseOrders' && <OpenPurchaseOrdersReport />}
            {selected === 'unpaidInvoices' && <UnpaidInvoicesReport />}
            {selected === 'purchaseReturns' && <PurchaseReturnsReport />}
            {selected === 'supplierEvaluationRanking' && <SupplierEvaluationRankingReport />}
            {selected === 'purchaseExpenses' && <PurchaseExpensesReport />}
            {selected === 'expiringContracts' && <ExpiringContractsReport />}
            {selected === 'additionalCostAllocation' && <AdditionalCostAllocationReport />}
            {selected === 'expiredQuotes' && <ExpiredQuotesReport />}
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

function DateRangeReport<TRow>({
  title, useReportHook, columns, renderTable
}: {
  title: string;
  useReportHook: (from: string, to: string, enabled: boolean) => { data: TRow[] | undefined; isFetching: boolean };
  columns: ExportColumn<TRow>[];
  renderTable: (data: TRow[]) => React.ReactNode;
}) {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useReportHook(from, to, run);

  return (
    <div>
      <ReportHeader title={title}>
        <FieldInline label={label('from', t('purchasingReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('purchasingReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('purchasingReports.run')}</Button>
        {data && <ExportMenu rows={data} columns={columns} fileName={title} title={title} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && <div style={{ overflowX: 'auto' }}>{renderTable(data)}</div>}
    </div>
  );
}

function PurchasesBySupplierReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<PurchasesBySupplierRow>[] = [
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('currencyCode', t('purchasingReports.currencyCode')), value: (r) => r.currencyCode },
    { header: label('invoiceCount', t('purchasingReports.invoiceCount')), value: (r) => r.invoiceCount },
    { header: label('totalAmount', t('purchasingReports.totalAmount')), value: (r) => r.totalAmount },
    { header: label('totalPaid', t('purchasingReports.totalPaid')), value: (r) => r.totalPaid }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.purchasesBySupplier')}
      useReportHook={usePurchasesBySupplierReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('currencyCode', t('purchasingReports.currencyCode'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('invoiceCount', t('purchasingReports.invoiceCount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalAmount', t('purchasingReports.totalAmount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalPaid', t('purchasingReports.totalPaid'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={`${r.supplierId}-${r.currencyCode}`}>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}>{r.currencyCode}</td>
                <td style={{ padding: 6 }}>{r.invoiceCount}</td>
                <td style={{ padding: 6 }}>{r.totalAmount.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.totalPaid.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function PurchasesByItemReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<PurchasesByItemRow>[] = [
    { header: label('item', t('purchasingReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('totalQuantity', t('purchasingReports.totalQuantity')), value: (r) => r.totalQuantity },
    { header: label('totalValue', t('purchasingReports.totalValue')), value: (r) => r.totalValue },
    { header: label('averageUnitPrice', t('purchasingReports.averageUnitPrice')), value: (r) => r.averageUnitPrice }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.purchasesByItem')}
      useReportHook={usePurchasesByItemReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('purchasingReports.item'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalQuantity', t('purchasingReports.totalQuantity'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalValue', t('purchasingReports.totalValue'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('averageUnitPrice', t('purchasingReports.averageUnitPrice'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.itemId}>
                <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                <td style={{ padding: 6 }}>{r.totalQuantity.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.totalValue.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.averageUnitPrice.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function OpenPurchaseOrdersReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<OpenPurchaseOrderRow>[] = [
    { header: label('orderNumber', t('purchasingReports.orderNumber')), value: (r) => r.orderNumber },
    { header: label('orderDate', t('purchasingReports.orderDate')), value: (r) => r.orderDate },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('status', t('purchasingReports.status')), value: (r) => r.status },
    { header: label('totalAmount', t('purchasingReports.totalAmount')), value: (r) => r.totalAmount },
    { header: label('remainingQuantity', t('purchasingReports.remainingQuantity')), value: (r) => r.remainingQuantity }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.openPurchaseOrders')}
      useReportHook={useOpenPurchaseOrdersReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('orderNumber', t('purchasingReports.orderNumber'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('orderDate', t('purchasingReports.orderDate'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('status', t('purchasingReports.status'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalAmount', t('purchasingReports.totalAmount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('remainingQuantity', t('purchasingReports.remainingQuantity'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 6 }}>{r.orderNumber}</td>
                <td style={{ padding: 6 }}>{r.orderDate}</td>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}><StatusBadge status={r.status} /></td>
                <td style={{ padding: 6 }}>{r.totalAmount.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.remainingQuantity.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function UnpaidInvoicesReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<UnpaidPurchaseInvoiceRow>[] = [
    { header: label('invoiceNumber', t('purchasingReports.invoiceNumber')), value: (r) => r.invoiceNumber },
    { header: label('dueDate', t('purchasingReports.dueDate')), value: (r) => r.dueDate },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('totalAmount', t('purchasingReports.totalAmount')), value: (r) => r.totalAmount },
    { header: label('remainingAmount', t('purchasingReports.remainingAmount')), value: (r) => r.remainingAmount },
    { header: label('daysOverdue', t('purchasingReports.daysOverdue')), value: (r) => r.daysOverdue }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.unpaidInvoices')}
      useReportHook={useUnpaidInvoicesReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('invoiceNumber', t('purchasingReports.invoiceNumber'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('dueDate', t('purchasingReports.dueDate'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalAmount', t('purchasingReports.totalAmount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('remainingAmount', t('purchasingReports.remainingAmount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('daysOverdue', t('purchasingReports.daysOverdue'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 6 }}>{r.invoiceNumber}</td>
                <td style={{ padding: 6 }}>{r.dueDate}</td>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}>{r.totalAmount.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.remainingAmount.toFixed(2)}</td>
                <td style={{ padding: 6, color: r.daysOverdue > 0 ? 'var(--color-error)' : undefined, fontWeight: r.daysOverdue > 0 ? 700 : undefined }}>{r.daysOverdue}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function PurchaseReturnsReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<PurchaseReturnReportRow>[] = [
    { header: label('returnNumber', t('purchasingReports.returnNumber')), value: (r) => r.returnNumber },
    { header: label('returnDate', t('purchasingReports.returnDate')), value: (r) => r.returnDate },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('reason', t('purchasingReports.reason')), value: (r) => r.reason },
    { header: label('totalValue', t('purchasingReports.totalValue')), value: (r) => r.totalValue }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.purchaseReturns')}
      useReportHook={usePurchaseReturnsReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('returnNumber', t('purchasingReports.returnNumber'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('returnDate', t('purchasingReports.returnDate'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('reason', t('purchasingReports.reason'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('status', t('purchasingReports.status'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('totalValue', t('purchasingReports.totalValue'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.id}>
                <td style={{ padding: 6 }}>{r.returnNumber}</td>
                <td style={{ padding: 6 }}>{r.returnDate}</td>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}>{t(`purchaseReturns.reason${r.reason}`)}</td>
                <td style={{ padding: 6 }}><StatusBadge status={r.status} /></td>
                <td style={{ padding: 6 }}>{r.totalValue.toFixed(2)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function SupplierEvaluationRankingReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<SupplierEvaluationRankingRow>[] = [
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('evaluationCount', t('purchasingReports.evaluationCount')), value: (r) => r.evaluationCount },
    { header: label('averageOverallScore', t('purchasingReports.averageOverallScore')), value: (r) => r.averageOverallScore },
    { header: label('latestEvaluationDate', t('purchasingReports.latestEvaluationDate')), value: (r) => r.latestEvaluationDate }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.supplierEvaluationRanking')}
      useReportHook={useSupplierEvaluationRankingReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('evaluationCount', t('purchasingReports.evaluationCount'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('averageOverallScore', t('purchasingReports.averageOverallScore'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('latestEvaluationDate', t('purchasingReports.latestEvaluationDate'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.supplierId}>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}>{r.evaluationCount}</td>
                <td style={{ padding: 6, fontWeight: 700, color: r.averageOverallScore >= 80 ? 'var(--color-success)' : r.averageOverallScore >= 60 ? 'var(--color-warning)' : 'var(--color-error)' }}>
                  {r.averageOverallScore.toFixed(1)}
                </td>
                <td style={{ padding: 6 }}>{r.latestEvaluationDate}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function PurchaseExpensesReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = usePurchaseExpensesReport(from, to, run);
  const { data: byType } = usePurchaseExpensesByTypeReport(from, to, run);
  const columns: ExportColumn<PurchaseExpenseReportRow>[] = [
    { header: label('invoiceNumber', t('purchasingReports.invoiceNumber')), value: (r) => r.invoiceNumber },
    { header: label('invoiceDate', t('purchasingReports.invoiceDate')), value: (r) => r.invoiceDate },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('additionalCosts', t('purchasingReports.additionalCosts')), value: (r) => r.additionalCosts },
    { header: label('allocationMethod', t('purchasingReports.allocationMethod')), value: (r) => r.allocationMethod ?? '' }
  ];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <div>
        <ReportHeader title={t('purchasingReports.purchaseExpenses')}>
          <FieldInline label={label('from', t('purchasingReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
          <FieldInline label={label('to', t('purchasingReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
          <Button variant="primary" onClick={() => setRun(true)}>{t('purchasingReports.run')}</Button>
          {data && <ExportMenu rows={data} columns={columns} fileName={t('purchasingReports.purchaseExpenses')} title={t('purchasingReports.purchaseExpenses')} />}
        </ReportHeader>
        {isFetching && <p>{t('common.loading')}</p>}
        {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
        {data && data.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', fontSize: 13 }}>
              <thead><tr>
                <th style={{ textAlign: 'start', padding: 6 }}>{label('invoiceNumber', t('purchasingReports.invoiceNumber'))}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{label('invoiceDate', t('purchasingReports.invoiceDate'))}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{label('additionalCosts', t('purchasingReports.additionalCosts'))}</th>
                <th style={{ textAlign: 'start', padding: 6 }}>{label('allocationMethod', t('purchasingReports.allocationMethod'))}</th>
              </tr></thead>
              <tbody>
                {data.map((r) => (
                  <tr key={r.id}>
                    <td style={{ padding: 6 }}>{r.invoiceNumber}</td>
                    <td style={{ padding: 6 }}>{r.invoiceDate}</td>
                    <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                    <td style={{ padding: 6 }}>{r.additionalCosts.toFixed(2)}</td>
                    <td style={{ padding: 6 }}>{r.allocationMethod ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {byType && byType.length > 0 && (
        <div>
          <h4 style={{ fontSize: 13 }}>{t('purchasingReports.byExpenseType')}</h4>
          <table style={{ fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('purchaseExpenses.expenseType')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.invoiceCount')}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.additionalCosts')}</th>
            </tr></thead>
            <tbody>
              {byType.map((r) => (
                <tr key={r.expenseType}>
                  <td style={{ padding: 6 }}>{t(`purchaseExpenses.type${r.expenseType}`)}</td>
                  <td style={{ padding: 6 }}>{r.count}</td>
                  <td style={{ padding: 6 }}>{r.totalAmount.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ExpiredQuotesReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const columns: ExportColumn<ExpiredRFQQuoteRow>[] = [
    { header: label('rfqNumber', t('purchasingReports.rfqNumber')), value: (r) => r.rfqNumber },
    { header: label('item', t('purchasingReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('unitPrice', t('purchasingReports.unitPrice')), value: (r) => r.unitPrice },
    { header: label('validUntil', t('purchasingReports.validUntil')), value: (r) => r.validUntil },
    { header: label('daysExpired', t('purchasingReports.daysExpired')), value: (r) => r.daysExpired }
  ];
  return (
    <DateRangeReport
      title={t('purchasingReports.expiredQuotes')}
      useReportHook={useExpiredQuotesReport}
      columns={columns}
      renderTable={(data) => (
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead><tr>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('rfqNumber', t('purchasingReports.rfqNumber'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('item', t('purchasingReports.item'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('unitPrice', t('purchasingReports.unitPrice'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('validUntil', t('purchasingReports.validUntil'))}</th>
            <th style={{ textAlign: 'start', padding: 6 }}>{label('daysExpired', t('purchasingReports.daysExpired'))}</th>
          </tr></thead>
          <tbody>
            {data.map((r) => (
              <tr key={r.quoteId}>
                <td style={{ padding: 6 }}>{r.rfqNumber}</td>
                <td style={{ padding: 6 }}>{r.itemCode} — {r.itemNameAr}</td>
                <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                <td style={{ padding: 6 }}>{r.unitPrice.toFixed(2)}</td>
                <td style={{ padding: 6 }}>{r.validUntil}</td>
                <td style={{ padding: 6, color: 'var(--color-error)', fontWeight: 700 }}>{r.daysExpired}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    />
  );
}

function ExpiringContractsReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const [withinDays, setWithinDays] = useState(30);
  const { data, isFetching } = useExpiringContractsReport(withinDays);
  const columns: ExportColumn<ExpiringSupplierContractRow>[] = [
    { header: label('contractNumber', t('purchasingReports.contractNumber')), value: (r) => r.contractNumber },
    { header: label('supplier', t('purchasingReports.supplier')), value: (r) => `${r.supplierCode} — ${r.supplierNameAr}` },
    { header: label('endDate', t('purchasingReports.endDate')), value: (r) => r.endDate },
    { header: label('daysRemaining', t('purchasingReports.daysRemaining')), value: (r) => r.daysRemaining }
  ];

  return (
    <div>
      <ReportHeader title={t('purchasingReports.expiringContracts')}>
        <FieldInline label={label('withinDays', t('purchasingReports.withinDays'))}>
          <Input type="number" value={withinDays} onChange={(e) => setWithinDays(Number(e.target.value))} style={{ width: 100 }} />
        </FieldInline>
        {data && <ExportMenu rows={data} columns={columns} fileName={t('purchasingReports.expiringContracts')} title={t('purchasingReports.expiringContracts')} />}
      </ReportHeader>
      {isFetching && <p>{t('common.loading')}</p>}
      {data && data.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
      {data && data.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', fontSize: 13 }}>
            <thead><tr>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('contractNumber', t('purchasingReports.contractNumber'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('supplier', t('purchasingReports.supplier'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('endDate', t('purchasingReports.endDate'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('daysRemaining', t('purchasingReports.daysRemaining'))}</th>
              <th style={{ textAlign: 'start', padding: 6 }}>{label('autoRenew', t('purchasingReports.autoRenew'))}</th>
            </tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td style={{ padding: 6 }}>{r.contractNumber}</td>
                  <td style={{ padding: 6 }}>{r.supplierCode} — {r.supplierNameAr}</td>
                  <td style={{ padding: 6 }}>{r.endDate}</td>
                  <td style={{ padding: 6, color: r.daysRemaining < 0 ? 'var(--color-error)' : 'var(--color-warning)', fontWeight: 700 }}>{r.daysRemaining}</td>
                  <td style={{ padding: 6 }}>{r.autoRenew ? t('common.yes') : t('common.no')}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function AdditionalCostAllocationReport() {
  const { t } = useTranslation();
  const { label } = useFieldLabels('PURCHASING_REPORTS');
  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(today);
  const [run, setRun] = useState(false);
  const { data, isFetching } = useAdditionalCostAllocationReport(from, to, run);
  const itemColumns: ExportColumn<AllocationByItemRow>[] = [
    { header: label('item', t('purchasingReports.item')), value: (r) => `${r.itemCode} — ${r.itemNameAr}` },
    { header: label('totalAllocatedAdditionalCost', t('purchasingReports.totalAllocatedAdditionalCost')), value: (r) => r.totalAllocatedAdditionalCost }
  ];

  return (
    <div>
      <ReportHeader title={t('purchasingReports.additionalCostAllocation')}>
        <FieldInline label={label('from', t('purchasingReports.from'))}><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FieldInline>
        <FieldInline label={label('to', t('purchasingReports.to'))}><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FieldInline>
        <Button variant="primary" onClick={() => setRun(true)}>{t('purchasingReports.run')}</Button>
        {data && <ExportMenu rows={data.byItem} columns={itemColumns} fileName={t('purchasingReports.additionalCostAllocation')} title={t('purchasingReports.additionalCostAllocation')} />}
      </ReportHeader>

      {isFetching && <p>{t('common.loading')}</p>}
      {data && (
        <div style={{ display: 'flex', gap: 32, flexWrap: 'wrap' }}>
          <div>
            <h4 style={{ fontSize: 13 }}>{label('byMethod', t('purchasingReports.byMethod'))}</h4>
            {data.byMethod.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
            {data.byMethod.length > 0 && (
              <table style={{ fontSize: 13 }}>
                <thead><tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.allocationMethod')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.invoiceCount')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.additionalCosts')}</th>
                </tr></thead>
                <tbody>
                  {data.byMethod.map((m) => (
                    <tr key={m.allocationMethod}>
                      <td style={{ padding: 6 }}>{m.allocationMethod}</td>
                      <td style={{ padding: 6 }}>{m.invoiceCount}</td>
                      <td style={{ padding: 6 }}>{m.totalAdditionalCosts.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
          <div>
            <h4 style={{ fontSize: 13 }}>{label('byItem', t('purchasingReports.byItem'))}</h4>
            {data.byItem.length === 0 && <p style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>{t('common.noData')}</p>}
            {data.byItem.length > 0 && (
              <table style={{ fontSize: 13 }}>
                <thead><tr>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.item')}</th>
                  <th style={{ textAlign: 'start', padding: 6 }}>{t('purchasingReports.totalAllocatedAdditionalCost')}</th>
                </tr></thead>
                <tbody>
                  {data.byItem.map((i) => (
                    <tr key={i.itemId}>
                      <td style={{ padding: 6 }}>{i.itemCode} — {i.itemNameAr}</td>
                      <td style={{ padding: 6 }}>{i.totalAllocatedAdditionalCost.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
