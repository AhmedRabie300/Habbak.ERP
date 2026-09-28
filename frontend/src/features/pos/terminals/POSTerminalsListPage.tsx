import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { usePOSTerminalsList } from './api';
import type { POSTerminal } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /pos/terminals — screen مبدئي (05-Module-POS-Shifts.md, section 2.1). Not server-paginated —
 * a company's POS-terminal count is always small, same reasoning as Suppliers/CustodyOfficers. */
export function POSTerminalsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: terminals, isLoading } = usePOSTerminalsList();
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('POS_TERMINALS');

  const branchName = (branchId: number) => branches?.find((b) => b.id === branchId)?.nameAr ?? branchId;

  const filtered = (terminals ?? []).filter((termi) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return termi.code.toLowerCase().includes(query) || termi.nameAr.toLowerCase().includes(query) || termi.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<POSTerminal> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<POSTerminal>[] = [
    { key: 'code', label: label('code', t('posTerminals.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('posTerminals.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('posTerminals.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'branchId', label: label('branch', t('posTerminals.branch')), render: (r) => branchName(r.branchId), exportValue: (r) => String(branchName(r.branchId)) },
    { key: 'isActive', label: label('isActive', t('posTerminals.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('posTerminals.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/pos/terminals/new')}>{t('posTerminals.addTerminal')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/terminals/${row.id}`)}
        exportFileName={t('posTerminals.title')}
      />
    </div>
  );
}
