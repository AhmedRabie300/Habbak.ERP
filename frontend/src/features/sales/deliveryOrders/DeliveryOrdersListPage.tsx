import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useDeliveryOrdersList } from './api';
import type { DeliveryOrderListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/delivery-orders — screen #8 (04-Module-Sales.md, section 5). */
export function DeliveryOrdersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useDeliveryOrdersList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('SALES_DELIVERY_ORDER');

  const columns: DataGridColumn<DeliveryOrderListItem>[] = [
    { key: 'deliveryNumber', label: label('deliveryNumber', t('deliveryOrders.deliveryNumber')), render: (r) => r.deliveryNumber, exportValue: (r) => r.deliveryNumber },
    { key: 'deliveryDate', label: label('deliveryDate', t('deliveryOrders.deliveryDate')), render: (r) => r.deliveryDate, exportValue: (r) => r.deliveryDate },
    { key: 'customer', label: label('customer', t('deliveryOrders.customer')), render: (r) => r.customerNameAr, exportValue: (r) => r.customerNameAr },
    { key: 'warehouse', label: label('warehouse', t('deliveryOrders.warehouse')), render: (r) => r.warehouseNameAr, exportValue: (r) => r.warehouseNameAr },
    { key: 'lineCount', label: label('lineCount', t('deliveryOrders.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('deliveryOrders.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('deliveryOrders.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/delivery-orders/new')}>{t('deliveryOrders.addDeliveryOrder')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/sales/delivery-orders/${row.id}`)}
        exportFileName={t('deliveryOrders.title')}
      />
    </div>
  );
}
