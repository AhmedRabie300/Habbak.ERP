import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useAccountsList } from '../accounts/api';
import { useTreasuryTransfersList } from './api';
import type { TreasuryTransferListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/treasury-transfers (01-Module-Accounting.md, section 5, screen 7). */
export function TreasuryTransfersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useTreasuryTransfersList({ search, page, pageSize: 25 });
  const { data: accounts } = useAccountsList(true);
  const { label } = useFieldLabels('ACCOUNTING_TREASURY_TRANSFERS');
  const accountLabel = (id: number) => {
    const account = accounts?.find((a) => a.id === id);
    return account ? `${account.code} - ${account.nameAr}` : id;
  };

  const columns: DataGridColumn<TreasuryTransferListItem>[] = [
    { key: 'transferDate', label: label('date', t('treasuryTransfers.date')), render: (r) => r.transferDate, exportValue: (r) => r.transferDate },
    { key: 'from', label: label('fromAccount', t('treasuryTransfers.fromAccount')), render: (r) => accountLabel(r.fromTreasuryAccountId), exportValue: (r) => accountLabel(r.fromTreasuryAccountId) },
    { key: 'to', label: label('toAccount', t('treasuryTransfers.toAccount')), render: (r) => accountLabel(r.toTreasuryAccountId), exportValue: (r) => accountLabel(r.toTreasuryAccountId) },
    { key: 'amount', label: label('amount', t('treasuryTransfers.amount')), render: (r) => r.amount.toLocaleString(), exportValue: (r) => r.amount },
    { key: 'status', label: label('status', t('treasuryTransfers.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('treasuryTransfers.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/treasury-transfers/new')}>{t('treasuryTransfers.newTransfer')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/accounting/treasury-transfers/${row.id}`)}
        exportFileName={t('treasuryTransfers.title')}
      />
    </div>
  );
}
