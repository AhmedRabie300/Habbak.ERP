import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useGoodsReceiptsList } from './api';
import type { GoodsReceiptListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/goods-receipts — screen #6 (03-Module-Purchasing.md, section 8). */
export function GoodsReceiptsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useGoodsReceiptsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_GOODS_RECEIPT');

  const columns: DataGridColumn<GoodsReceiptListItem>[] = [
    { key: 'receiptNumber', label: label('receiptNumber', t('goodsReceipts.receiptNumber')), render: (r) => r.receiptNumber, exportValue: (r) => r.receiptNumber },
    { key: 'receiptDate', label: label('receiptDate', t('goodsReceipts.receiptDate')), render: (r) => r.receiptDate, exportValue: (r) => r.receiptDate },
    { key: 'warehouse', label: label('warehouse', t('goodsReceipts.warehouse')), render: (r) => r.warehouseCode, exportValue: (r) => r.warehouseCode },
    { key: 'supplier', label: label('supplier', t('goodsReceipts.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'lineCount', label: label('lineCount', t('goodsReceipts.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('goodsReceipts.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('goodsReceipts.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/goods-receipts/new')}>{t('goodsReceipts.addGoodsReceipt')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/goods-receipts/${row.id}`)}
        exportFileName={t('goodsReceipts.title')}
      />
    </div>
  );
}
