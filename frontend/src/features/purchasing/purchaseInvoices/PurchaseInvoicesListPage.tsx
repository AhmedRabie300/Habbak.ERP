import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePurchaseInvoicesList } from './api';
import type { PurchaseInvoiceListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/purchase-invoices — screen #5 (03-Module-Purchasing.md, section 8). */
export function PurchaseInvoicesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = usePurchaseInvoicesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_INVOICE');

  const columns: DataGridColumn<PurchaseInvoiceListItem>[] = [
    { key: 'invoiceNumber', label: label('invoiceNumber', t('purchaseInvoices.invoiceNumber')), render: (r) => r.invoiceNumber, exportValue: (r) => r.invoiceNumber },
    { key: 'invoiceDate', label: label('invoiceDate', t('purchaseInvoices.invoiceDate')), render: (r) => r.invoiceDate, exportValue: (r) => r.invoiceDate },
    { key: 'dueDate', label: label('dueDate', t('purchaseInvoices.dueDate')), render: (r) => r.dueDate, exportValue: (r) => r.dueDate },
    { key: 'supplier', label: label('supplier', t('purchaseInvoices.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'totalAmount', label: label('totalAmount', t('purchaseInvoices.totalAmount')), render: (r) => `${r.totalAmount.toFixed(2)} ${r.currencyCode}`, exportValue: (r) => r.totalAmount },
    { key: 'lineCount', label: label('lineCount', t('purchaseInvoices.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('purchaseInvoices.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('purchaseInvoices.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/purchase-invoices/new')}>{t('purchaseInvoices.addPurchaseInvoice')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/purchase-invoices/${row.id}`)}
        exportFileName={t('purchaseInvoices.title')}
      />
    </div>
  );
}
