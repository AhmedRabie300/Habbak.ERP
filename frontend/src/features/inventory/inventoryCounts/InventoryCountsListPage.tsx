import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useInventoryCountsList } from './api';
import type { InventoryCountListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/inventory-counts — screen #14 (02-Module-Inventory-Manufacturing.md, section 5),
 * the multi-stage count cycle's entry point. Every row (whatever its stage) opens the same
 * InventoryCountEditPage, which switches its own layout by Status. */
export function InventoryCountsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useInventoryCountsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('INVENTORY_COUNT');

  const columns: DataGridColumn<InventoryCountListItem>[] = [
    { key: 'countNumber', label: label('countNumber', t('inventoryCounts.countNumber')), render: (r) => r.countNumber, exportValue: (r) => r.countNumber },
    { key: 'countDate', label: label('countDate', t('inventoryCounts.countDate')), render: (r) => r.countDate, exportValue: (r) => r.countDate },
    { key: 'warehouse', label: label('warehouse', t('inventoryCounts.warehouse')), render: (r) => r.warehouseCode, exportValue: (r) => r.warehouseCode },
    { key: 'countType', label: label('countType', t('inventoryCounts.countType')), render: (r) => t(`inventoryCounts.type${r.countType}`), exportValue: (r) => r.countType },
    { key: 'lineCount', label: label('lineCount', t('inventoryCounts.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('inventoryCounts.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('inventoryCounts.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/inventory-counts/new')}>{t('inventoryCounts.addInventoryCount')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/inventory-counts/${row.id}`)}
        exportFileName={t('inventoryCounts.title')}
      />
    </div>
  );
}
