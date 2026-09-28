import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePeriodsList } from './api';
import type { PeriodListItem } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/accounting-periods — My Remarks/Remarks2.md, remark 3.1: the standard List/Edit
 * pair, replacing the old dual-panel single-page screen. Not server-paginated (a company's
 * period count grows by at most one a month), same convention as ItemGroupsListPage. */
export function PeriodsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: periods, isLoading } = usePeriodsList();
  const { label } = useFieldLabels('ACCOUNTING_PERIODS');

  const filtered = (periods ?? []).filter((p) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return p.periodStart.toLowerCase().includes(query) || p.periodEnd.toLowerCase().includes(query) || p.status.toLowerCase().includes(query);
  });

  const data: PagedResult<PeriodListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<PeriodListItem>[] = [
    { key: 'periodStart', label: label('from', t('periods.from')), render: (r) => r.periodStart, exportValue: (r) => r.periodStart },
    { key: 'periodEnd', label: label('to', t('periods.to')), render: (r) => r.periodEnd, exportValue: (r) => r.periodEnd },
    { key: 'status', label: t('periods.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('periods.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/accounting-periods/new')}>{t('periods.newPeriod')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/accounting/accounting-periods/${row.id}`)}
        exportFileName={t('periods.title')}
      />
    </div>
  );
}
