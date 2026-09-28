import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePurchaseOrdersList } from './api';
import type { PurchaseOrderListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/purchase-orders — screen #4 (03-Module-Purchasing.md, section 8). */
export function PurchaseOrdersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = usePurchaseOrdersList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_ORDER');

  const columns: DataGridColumn<PurchaseOrderListItem>[] = [
    { key: 'orderNumber', label: label('orderNumber', t('purchaseOrders.orderNumber')), render: (r) => r.orderNumber, exportValue: (r) => r.orderNumber },
    { key: 'orderDate', label: label('orderDate', t('purchaseOrders.orderDate')), render: (r) => r.orderDate, exportValue: (r) => r.orderDate },
    { key: 'supplier', label: label('supplier', t('purchaseOrders.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'totalAmount', label: label('totalAmount', t('purchaseOrders.totalAmount')), render: (r) => `${r.totalAmount.toFixed(2)} ${r.currencyCode}`, exportValue: (r) => r.totalAmount },
    { key: 'lineCount', label: label('lineCount', t('purchaseOrders.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('purchaseOrders.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('purchaseOrders.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/purchase-orders/new')}>{t('purchaseOrders.addPurchaseOrder')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/purchase-orders/${row.id}`)}
        exportFileName={t('purchaseOrders.title')}
      />
    </div>
  );
}
