import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useProductionOrdersList } from './api';
import type { ProductionOrderListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/production-orders — screen #19 (02-Module-Inventory-Manufacturing.md, section 5). */
export function ProductionOrdersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useProductionOrdersList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('INVENTORY_PRODUCTION_ORDER');

  const columns: DataGridColumn<ProductionOrderListItem>[] = [
    { key: 'orderNumber', label: label('orderNumber', t('productionOrders.orderNumber')), render: (r) => r.orderNumber, exportValue: (r) => r.orderNumber },
    { key: 'outputItem', label: label('outputItem', t('productionOrders.outputItem')), render: (r) => `${r.outputItemCode} — ${r.outputItemNameAr}`, exportValue: (r) => r.outputItemCode },
    { key: 'recipe', label: label('recipe', t('productionOrders.recipe')), render: (r) => `${r.recipeFamilyCode} (v${r.recipeVersionNumber})`, exportValue: (r) => r.recipeFamilyCode },
    { key: 'warehouse', label: label('warehouse', t('productionOrders.warehouse')), render: (r) => r.warehouseCode, exportValue: (r) => r.warehouseCode },
    { key: 'plannedQuantity', label: label('plannedQuantity', t('productionOrders.plannedQuantity')), render: (r) => r.plannedQuantity, exportValue: (r) => r.plannedQuantity },
    { key: 'actualQuantity', label: label('actualQuantity', t('productionOrders.actualQuantity')), render: (r) => r.actualQuantity ?? '—', exportValue: (r) => r.actualQuantity ?? '' },
    { key: 'status', label: label('status', t('productionOrders.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('productionOrders.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/production-orders/new')}>{t('productionOrders.addProductionOrder')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/production-orders/${row.id}`)}
        exportFileName={t('productionOrders.title')}
      />
    </div>
  );
}
