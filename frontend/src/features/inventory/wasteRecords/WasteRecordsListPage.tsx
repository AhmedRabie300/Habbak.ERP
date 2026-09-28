import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useWasteRecordsList } from './api';
import type { WasteRecordListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/waste-records — screen #18 (02-Module-Inventory-Manufacturing.md, section 5). */
export function WasteRecordsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useWasteRecordsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('INVENTORY_WASTE_RECORD');

  const columns: DataGridColumn<WasteRecordListItem>[] = [
    { key: 'wasteDate', label: label('wasteDate', t('wasteRecords.wasteDate')), render: (r) => r.wasteDate, exportValue: (r) => r.wasteDate },
    { key: 'item', label: label('item', t('wasteRecords.item')), render: (r) => `${r.itemCode} — ${r.itemNameAr}`, exportValue: (r) => r.itemCode },
    { key: 'warehouse', label: label('warehouse', t('wasteRecords.warehouse')), render: (r) => r.warehouseCode, exportValue: (r) => r.warehouseCode },
    { key: 'quantity', label: label('quantity', t('wasteRecords.quantity')), render: (r) => r.quantity, exportValue: (r) => r.quantity },
    { key: 'reason', label: label('reason', t('wasteRecords.reason')), render: (r) => r.reason, exportValue: (r) => r.reason },
    { key: 'source', label: label('source', t('wasteRecords.source')), render: (r) => r.sourceDocumentType ?? '—', exportValue: (r) => r.sourceDocumentType ?? '' }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('wasteRecords.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/waste-records/new')}>{t('wasteRecords.addWasteRecord')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/waste-records/${row.id}`)}
        exportFileName={t('wasteRecords.title')}
      />
    </div>
  );
}
