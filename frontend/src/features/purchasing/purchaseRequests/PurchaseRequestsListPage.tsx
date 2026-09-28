import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { usePurchaseRequestsList } from './api';
import type { PurchaseRequestListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/purchase-requests — screen #2 (03-Module-Purchasing.md, section 8). */
export function PurchaseRequestsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = usePurchaseRequestsList({ search, page, pageSize: 25 });
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('PURCHASING_PURCHASE_REQUEST');

  const branchName = (branchId?: number) => (branches ?? []).find((b) => b.id === branchId)?.nameAr ?? '';

  const columns: DataGridColumn<PurchaseRequestListItem>[] = [
    { key: 'requestNumber', label: label('requestNumber', t('purchaseRequests.requestNumber')), render: (r) => r.requestNumber, exportValue: (r) => r.requestNumber },
    { key: 'requestDate', label: label('requestDate', t('purchaseRequests.requestDate')), render: (r) => r.requestDate, exportValue: (r) => r.requestDate },
    { key: 'branch', label: label('branch', t('purchaseRequests.branch')), render: (r) => branchName(r.branchId) || '—', exportValue: (r) => branchName(r.branchId) },
    { key: 'priority', label: label('priority', t('purchaseRequests.priority')), render: (r) => t(`purchaseRequests.priority${r.priority}`), exportValue: (r) => r.priority },
    { key: 'lineCount', label: label('lineCount', t('purchaseRequests.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('purchaseRequests.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('purchaseRequests.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/purchase-requests/new')}>{t('purchaseRequests.addPurchaseRequest')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/purchase-requests/${row.id}`)}
        exportFileName={t('purchaseRequests.title')}
      />
    </div>
  );
}
