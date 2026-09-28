import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSalesReturnsList } from './api';
import type { SalesReturnListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/returns — screen #9 (04-Module-Sales.md, section 5). */
export function SalesReturnsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSalesReturnsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('SALES_RETURN');

  const columns: DataGridColumn<SalesReturnListItem>[] = [
    { key: 'returnNumber', label: label('returnNumber', t('salesReturns.returnNumber')), render: (r) => r.returnNumber, exportValue: (r) => r.returnNumber },
    { key: 'returnDate', label: label('returnDate', t('salesReturns.returnDate')), render: (r) => r.returnDate, exportValue: (r) => r.returnDate },
    { key: 'customer', label: label('customer', t('salesReturns.customer')), render: (r) => r.customerNameAr, exportValue: (r) => r.customerNameAr },
    { key: 'warehouse', label: label('warehouse', t('salesReturns.warehouse')), render: (r) => r.warehouseNameAr, exportValue: (r) => r.warehouseNameAr },
    { key: 'reason', label: label('reason', t('salesReturns.reason')), render: (r) => r.reason, exportValue: (r) => r.reason },
    { key: 'lineCount', label: label('lineCount', t('salesReturns.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('salesReturns.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('salesReturns.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/returns/new')}>{t('salesReturns.addReturn')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/sales/returns/${row.id}`)}
        exportFileName={t('salesReturns.title')}
      />
    </div>
  );
}
