import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSalesOrdersList } from './api';
import type { SalesOrderListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/sales-orders — screen #6 (04-Module-Sales.md, section 5). */
export function SalesOrdersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSalesOrdersList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('SALES_ORDER');

  const columns: DataGridColumn<SalesOrderListItem>[] = [
    { key: 'orderNumber', label: label('orderNumber', t('salesOrders.orderNumber')), render: (r) => r.orderNumber, exportValue: (r) => r.orderNumber },
    { key: 'orderDate', label: label('orderDate', t('salesOrders.orderDate')), render: (r) => r.orderDate, exportValue: (r) => r.orderDate },
    { key: 'customer', label: label('customer', t('salesOrders.customer')), render: (r) => r.customerNameAr, exportValue: (r) => r.customerNameAr },
    { key: 'subtotal', label: label('subtotal', t('salesOrders.subtotal')), render: (r) => r.subtotal.toFixed(2), exportValue: (r) => r.subtotal },
    { key: 'status', label: label('status', t('salesOrders.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('salesOrders.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/sales-orders/new')}>{t('salesOrders.addOrder')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/sales/sales-orders/${row.id}`)}
        exportFileName={t('salesOrders.title')}
      />
    </div>
  );
}
