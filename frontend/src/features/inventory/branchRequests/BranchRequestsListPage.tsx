import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { useBranchRequestsList } from './api';
import type { BranchRequestListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/branch-requests — screens #12-13 (02-Module-Inventory-Manufacturing.md, section 5).
 * A pending request's row links to the approval screen instead of the plain edit screen — approval
 * is a distinct action from editing a still-open Draft (rule/screen #13 is its own interactive UI). */
export function BranchRequestsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useBranchRequestsList({ search, page, pageSize: 25 });
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('INVENTORY_BRANCH_REQUEST');

  const branchName = (branchId?: number) => (branches ?? []).find((b) => b.id === branchId)?.nameAr ?? '';

  const columns: DataGridColumn<BranchRequestListItem>[] = [
    { key: 'requestNumber', label: label('requestNumber', t('branchRequests.requestNumber')), render: (r) => r.requestNumber, exportValue: (r) => r.requestNumber },
    { key: 'requestDate', label: label('requestDate', t('branchRequests.requestDate')), render: (r) => r.requestDate, exportValue: (r) => r.requestDate },
    { key: 'branch', label: label('branch', t('branchRequests.branch')), render: (r) => branchName(r.branchId) || '—', exportValue: (r) => branchName(r.branchId) },
    { key: 'lineCount', label: label('lineCount', t('branchRequests.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'status', label: label('status', t('branchRequests.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('branchRequests.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/branch-requests/new')}>{t('branchRequests.addBranchRequest')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(row.status === 'PendingApproval' ? `/inventory/branch-requests/${row.id}/approve` : `/inventory/branch-requests/${row.id}`)}
        exportFileName={t('branchRequests.title')}
      />
    </div>
  );
}
