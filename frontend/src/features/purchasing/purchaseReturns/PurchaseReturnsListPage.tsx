import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePurchaseReturnsList } from './api';
import type { PurchaseReturnListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/purchase-returns — screen #7 (03-Module-Purchasing.md, section 8). */
export function PurchaseReturnsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = usePurchaseReturnsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_RETURN');

  const columns: DataGridColumn<PurchaseReturnListItem>[] = [
    { key: 'returnNumber', label: label('returnNumber', t('purchaseReturns.returnNumber')), render: (r) => r.returnNumber, exportValue: (r) => r.returnNumber },
    { key: 'returnDate', label: label('returnDate', t('purchaseReturns.returnDate')), render: (r) => r.returnDate, exportValue: (r) => r.returnDate },
    { key: 'supplier', label: label('supplier', t('purchaseReturns.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'reason', label: label('reason', t('purchaseReturns.reason')), render: (r) => t(`purchaseReturns.reason${r.reason}`), exportValue: (r) => r.reason },
    { key: 'lineCount', label: label('lineCount', t('purchaseReturns.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('purchaseReturns.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('purchaseReturns.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/purchase-returns/new')}>{t('purchaseReturns.addPurchaseReturn')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/purchase-returns/${row.id}`)}
        exportFileName={t('purchaseReturns.title')}
      />
    </div>
  );
}
