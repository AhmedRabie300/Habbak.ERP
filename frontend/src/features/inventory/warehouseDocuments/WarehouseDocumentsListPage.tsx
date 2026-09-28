import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useWarehouseDocumentsList } from './api';
import type { WarehouseDocumentKind, WarehouseDocumentListItem } from './types';

const KIND_META: Record<WarehouseDocumentKind, { titleKey: string; addKey: string; screenCode: string }> = {
  'stock-in': { titleKey: 'warehouseDocuments.stockInTitle', addKey: 'warehouseDocuments.addStockIn', screenCode: 'INVENTORY_STOCK_IN' },
  'stock-out': { titleKey: 'warehouseDocuments.stockOutTitle', addKey: 'warehouseDocuments.addStockOut', screenCode: 'INVENTORY_STOCK_OUT' },
  'transfer-order': { titleKey: 'warehouseDocuments.transferOrderTitle', addKey: 'warehouseDocuments.addTransferOrder', screenCode: 'INVENTORY_TRANSFER_ORDER' },
  'inventory-adjustments': { titleKey: 'warehouseDocuments.inventoryAdjustmentTitle', addKey: 'warehouseDocuments.addInventoryAdjustment', screenCode: 'INVENTORY_ADJUSTMENT' },
  'opening-balances': { titleKey: 'warehouseDocuments.openingBalanceTitle', addKey: 'warehouseDocuments.addOpeningBalance', screenCode: 'INVENTORY_OPENING_BALANCES' }
};

/** Shared List screen for /inventory/stock-in, /inventory/stock-out and /inventory/transfer-order
 * (02-Module-Inventory-Manufacturing.md, section 5, screens 8-10) — mirrors VouchersListPage's
 * kind-based sharing of one entity. */
export function WarehouseDocumentsListPage({ kind }: { kind: WarehouseDocumentKind }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useWarehouseDocumentsList(kind, { search, page, pageSize: 25 });
  const meta = KIND_META[kind];
  const title = t(meta.titleKey);
  const addLabel = t(meta.addKey);
  const { label } = useFieldLabels(meta.screenCode);

  const showsDestination = kind === 'stock-in' || kind === 'opening-balances';
  const isAdjustment = kind === 'inventory-adjustments';

  const columns: DataGridColumn<WarehouseDocumentListItem>[] = [
    { key: 'documentNumber', label: label('documentNumber', t('warehouseDocuments.documentNumber')), render: (r) => r.documentNumber, exportValue: (r) => r.documentNumber },
    { key: 'documentDate', label: label('documentDate', t('warehouseDocuments.documentDate')), render: (r) => r.documentDate, exportValue: (r) => r.documentDate },
    {
      key: 'warehouse',
      label: label('warehouse', isAdjustment ? t('warehouseDocuments.warehouse') : showsDestination ? t('warehouseDocuments.destinationWarehouse') : t('warehouseDocuments.sourceWarehouse')),
      render: (r) => (isAdjustment ? (r.destinationWarehouseCode ?? r.sourceWarehouseCode) : showsDestination ? r.destinationWarehouseCode : r.sourceWarehouseCode) ?? '—',
      exportValue: (r) => (isAdjustment ? (r.destinationWarehouseCode ?? r.sourceWarehouseCode) : showsDestination ? r.destinationWarehouseCode : r.sourceWarehouseCode) ?? ''
    },
    ...(kind === 'transfer-order'
      ? [{
          key: 'custodyOfficerCode',
          label: label('custodyOfficer', t('warehouseDocuments.custodyOfficer')),
          render: (r: WarehouseDocumentListItem) => r.custodyOfficerCode ?? '—',
          exportValue: (r: WarehouseDocumentListItem) => r.custodyOfficerCode ?? ''
        } as DataGridColumn<WarehouseDocumentListItem>]
      : []),
    { key: 'lineCount', label: label('lineCount', t('warehouseDocuments.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('warehouseDocuments.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{title}</h2>
        <Button variant="primary" onClick={() => navigate(`/inventory/${kind}/new`)}>{addLabel}</Button>
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/${kind}/${row.id}`)}
        exportFileName={title}
      />
    </div>
  );
}
