import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSalesQuotesList } from './api';
import type { SalesQuoteListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/quotes — screen #5 (04-Module-Sales.md, section 5). */
export function SalesQuotesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSalesQuotesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('SALES_QUOTE');

  const columns: DataGridColumn<SalesQuoteListItem>[] = [
    { key: 'quoteNumber', label: label('quoteNumber', t('salesQuotes.quoteNumber')), render: (r) => r.quoteNumber, exportValue: (r) => r.quoteNumber },
    { key: 'quoteDate', label: label('quoteDate', t('salesQuotes.quoteDate')), render: (r) => r.quoteDate, exportValue: (r) => r.quoteDate },
    { key: 'validUntil', label: label('validUntil', t('salesQuotes.validUntil')), render: (r) => r.validUntil, exportValue: (r) => r.validUntil },
    { key: 'customer', label: label('customer', t('salesQuotes.customer')), render: (r) => r.customerNameAr, exportValue: (r) => r.customerNameAr },
    { key: 'subtotal', label: label('subtotal', t('salesQuotes.subtotal')), render: (r) => r.subtotal.toFixed(2), exportValue: (r) => r.subtotal },
    { key: 'status', label: label('status', t('salesQuotes.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('salesQuotes.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/quotes/new')}>{t('salesQuotes.addQuote')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/sales/quotes/${row.id}`)}
        exportFileName={t('salesQuotes.title')}
      />
    </div>
  );
}
