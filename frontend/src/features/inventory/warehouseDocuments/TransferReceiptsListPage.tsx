import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useTransferReceiptsList } from './api';
import type { WarehouseDocumentListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/transfer-receipt — screen #11 (02-Module-Inventory-Manufacturing.md, section 5). */
export function TransferReceiptsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useTransferReceiptsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('INVENTORY_TRANSFER_RECEIPT');

  const columns: DataGridColumn<WarehouseDocumentListItem>[] = [
    { key: 'documentNumber', label: label('documentNumber', t('warehouseDocuments.documentNumber')), render: (r) => r.documentNumber, exportValue: (r) => r.documentNumber },
    { key: 'documentDate', label: label('documentDate', t('warehouseDocuments.documentDate')), render: (r) => r.documentDate, exportValue: (r) => r.documentDate },
    { key: 'destinationWarehouseCode', label: label('destinationWarehouse', t('warehouseDocuments.destinationWarehouse')), render: (r) => r.destinationWarehouseCode ?? '—', exportValue: (r) => r.destinationWarehouseCode ?? '' },
    { key: 'lineCount', label: label('lineCount', t('warehouseDocuments.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('warehouseDocuments.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('warehouseDocuments.transferReceiptTitle')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/transfer-receipt/new')}>{t('warehouseDocuments.addTransferReceipt')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/transfer-receipt/${row.id}`)}
        exportFileName={t('warehouseDocuments.transferReceiptTitle')}
      />
    </div>
  );
}
