import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSalesInvoicesList } from './api';
import type { SalesInvoiceListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/invoices — screen #7 (04-Module-Sales.md, section 5). Interactive per the spec (بانر
 * تحذير عند تجاوز حد الائتمان) — the warning itself lives on the Edit page since it only makes
 * sense at save time. */
export function SalesInvoicesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSalesInvoicesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('SALES_INVOICE');

  const columns: DataGridColumn<SalesInvoiceListItem>[] = [
    { key: 'invoiceNumber', label: label('invoiceNumber', t('salesInvoices.invoiceNumber')), render: (r) => r.invoiceNumber, exportValue: (r) => r.invoiceNumber },
    { key: 'invoiceDate', label: label('invoiceDate', t('salesInvoices.invoiceDate')), render: (r) => r.invoiceDate, exportValue: (r) => r.invoiceDate },
    { key: 'customer', label: label('customer', t('salesInvoices.customer')), render: (r) => r.customerNameAr, exportValue: (r) => r.customerNameAr },
    { key: 'paymentType', label: label('paymentType', t('salesInvoices.paymentType')), render: (r) => t(`salesInvoices.type${r.paymentType}`), exportValue: (r) => r.paymentType },
    { key: 'totalAmount', label: label('totalAmount', t('salesInvoices.totalAmount')), render: (r) => r.totalAmount.toFixed(2), exportValue: (r) => r.totalAmount },
    { key: 'amountPaid', label: label('amountPaid', t('salesInvoices.amountPaid')), render: (r) => r.amountPaid.toFixed(2), exportValue: (r) => r.amountPaid },
    { key: 'status', label: label('status', t('salesInvoices.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('salesInvoices.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/invoices/new')}>{t('salesInvoices.addInvoice')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/sales/invoices/${row.id}`)}
        exportFileName={t('salesInvoices.title')}
      />
    </div>
  );
}
