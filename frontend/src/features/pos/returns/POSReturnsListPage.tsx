import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { usePOSReturnsList } from './api';
import type { POSReturnListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /pos/returns — screen #6 (05-Module-POS-Shifts.md، قاعدة 16). */
export function POSReturnsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: returns, isLoading } = usePOSReturnsList();

  const filtered = (returns ?? []).filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return r.returnNumber.toLowerCase().includes(query) || r.sourceInvoiceNumber.toLowerCase().includes(query);
  });

  const data: PagedResult<POSReturnListItem> = {
    items: filtered, totalCount: filtered.length, page: 1, pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<POSReturnListItem>[] = [
    { key: 'returnNumber', label: t('posReturns.returnNumber'), render: (r) => r.returnNumber, exportValue: (r) => r.returnNumber },
    { key: 'sourceInvoiceNumber', label: t('posReturns.sourceInvoice'), render: (r) => r.sourceInvoiceNumber, exportValue: (r) => r.sourceInvoiceNumber },
    { key: 'returnDate', label: t('posReturns.returnDate'), render: (r) => r.returnDate, exportValue: (r) => r.returnDate },
    { key: 'reason', label: t('posReturns.reason'), render: (r) => r.reason, exportValue: (r) => r.reason },
    { key: 'total', label: t('posReturns.total'), render: (r) => r.total.toFixed(2), exportValue: (r) => r.total },
    { key: 'status', label: t('posReturns.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('posReturns.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/pos/returns/new')}>{t('posReturns.addReturn')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/returns/${row.id}`)}
        exportFileName={t('posReturns.title')}
      />
    </div>
  );
}
