import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { UserName, useUserNameOf } from '../../settings/security/UserName';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useShiftsList } from './api';
import { DayClosePanel } from './DayClosePanel';
import type { ShiftListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';

/** /pos/shifts — screens #1/#3/#14 (05-Module-POS-Shifts.md): سجل الورديات المفتوحة/المقفولة. */
export function ShiftsListPage() {
  const { t } = useTranslation();
  const nameOf = useUserNameOf();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const { data: shifts, isLoading } = useShiftsList();

  const filtered = (shifts ?? []).filter((s) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return s.posTerminalNameAr.toLowerCase().includes(query) || s.posTerminalNameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<ShiftListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<ShiftListItem>[] = [
    { key: 'posTerminalNameAr', label: t('shifts.terminal'), render: (r) => r.posTerminalNameAr, exportValue: (r) => r.posTerminalNameAr },
    { key: 'cashierUserId', label: t('shifts.cashierUserId'), render: (r) => <UserName id={r.cashierUserId} />, exportValue: (r) => nameOf(r.cashierUserId) },
    { key: 'status', label: t('shifts.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status },
    { key: 'openedAtUtc', label: t('shifts.openedAt'), render: (r) => new Date(r.openedAtUtc).toLocaleString(), exportValue: (r) => r.openedAtUtc },
    { key: 'openingCashAmount', label: t('shifts.openingCashAmount'), render: (r) => r.openingCashAmount.toFixed(2), exportValue: (r) => r.openingCashAmount },
    { key: 'differenceAmount', label: t('shifts.differenceAmount'), render: (r) => (r.differenceAmount != null ? r.differenceAmount.toFixed(2) : '—'), exportValue: (r) => r.differenceAmount ?? '' }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('shifts.title')}</h2>
        <Button variant="primary" onClick={() => navigate('/pos/shift-console')}>{t('shifts.openConsole')}</Button>
      </div>

      <DayClosePanel />

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/shifts/${row.id}`)}
        exportFileName={t('shifts.title')}
      />
    </div>
  );
}
