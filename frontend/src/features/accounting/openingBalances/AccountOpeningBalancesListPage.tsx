import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useAccountOpeningBalanceBatchesList } from './api';
import type { AccountOpeningBalanceBatchListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/opening-balances — My Remarks/Remarks2.md, bugs 1.4/3.9. */
export function AccountOpeningBalancesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useAccountOpeningBalanceBatchesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('ACCOUNTING_OPENING_BALANCES');

  const columns: DataGridColumn<AccountOpeningBalanceBatchListItem>[] = [
    { key: 'batchNumber', label: label('batchNumber', t('accountOpeningBalances.batchNumber')), render: (r) => r.batchNumber, exportValue: (r) => r.batchNumber },
    { key: 'transactionDate', label: label('transactionDate', t('accountOpeningBalances.transactionDate')), render: (r) => r.transactionDate, exportValue: (r) => r.transactionDate },
    { key: 'lineCount', label: label('lineCount', t('accountOpeningBalances.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'totalAmount', label: label('totalAmount', t('accountOpeningBalances.totalAmount')), render: (r) => r.totalAmount.toFixed(2), exportValue: (r) => r.totalAmount },
    { key: 'status', label: label('status', t('accountOpeningBalances.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('accountOpeningBalances.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/opening-balances/new')}>{t('accountOpeningBalances.addBatch')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/accounting/opening-balances/${row.id}`)}
        exportFileName={t('accountOpeningBalances.title')}
      />
    </div>
  );
}
