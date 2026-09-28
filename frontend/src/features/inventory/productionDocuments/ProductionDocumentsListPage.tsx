import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { useWarehouseDocumentsList } from '../warehouseDocuments/api';
import type { WarehouseDocumentListItem } from '../warehouseDocuments/types';

const KIND_META: Record<'production-issues' | 'production-receipts', { titleKey: string; warehouseKey: 'source' | 'destination' }> = {
  'production-issues': { titleKey: 'productionDocuments.issuesTitle', warehouseKey: 'source' },
  'production-receipts': { titleKey: 'productionDocuments.receiptsTitle', warehouseKey: 'destination' }
};

/** /inventory/production-issues and /inventory/production-receipts — screen #20
 * (02-Module-Inventory-Manufacturing.md, section 5): READ-ONLY. These WarehouseDocument rows are
 * only ever created by CompleteProductionOrderCommand, already Posted — no Create button, no
 * row-click into an editable form, just a trace of what a completed production order moved. */
export function ProductionDocumentsListPage({ kind }: { kind: 'production-issues' | 'production-receipts' }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useWarehouseDocumentsList(kind, { search, page, pageSize: 25 });
  const meta = KIND_META[kind];
  const showsDestination = meta.warehouseKey === 'destination';

  const columns: DataGridColumn<WarehouseDocumentListItem>[] = [
    { key: 'documentNumber', label: t('productionDocuments.documentNumber'), render: (r) => r.documentNumber, exportValue: (r) => r.documentNumber },
    { key: 'documentDate', label: t('productionDocuments.documentDate'), render: (r) => r.documentDate, exportValue: (r) => r.documentDate },
    {
      key: 'warehouse',
      label: t('productionDocuments.warehouse'),
      render: (r) => (showsDestination ? r.destinationWarehouseCode : r.sourceWarehouseCode) ?? '—',
      exportValue: (r) => (showsDestination ? r.destinationWarehouseCode : r.sourceWarehouseCode) ?? ''
    },
    { key: 'lineCount', label: t('productionDocuments.lineCount'), render: (r) => r.lineCount, exportValue: (r) => r.lineCount }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>{t(meta.titleKey)}</h2>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/${kind}/${row.id}`)}
        exportFileName={t(meta.titleKey)}
      />
    </div>
  );
}
