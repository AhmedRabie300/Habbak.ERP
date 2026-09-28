import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useCompaniesList } from './api';
import type { Company } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /settings/companies — the tenant root list (Company.cs doc). Not server-paginated (the
 * tenant list is always small) — search/pagination happen client-side, but the screen still
 * follows the standard List/Edit DataGrid pattern. */
export function CompaniesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: companies, isLoading } = useCompaniesList();

  const filtered = (companies ?? []).filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return c.code.toLowerCase().includes(query) || c.nameAr.toLowerCase().includes(query) || c.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<Company> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<Company>[] = [
    { key: 'code', label: t('companies.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('companies.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: t('companies.nameEn'), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'baseCurrency', label: t('companies.baseCurrency'), render: (r) => r.baseCurrencyCode, exportValue: (r) => r.baseCurrencyCode },
    { key: 'isActive', label: t('companies.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('companies.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/settings/companies/new')}>{t('companies.addCompany')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/settings/companies/${row.id}`)}
        exportFileName={t('companies.title')}
      />
    </div>
  );
}
