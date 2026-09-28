import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useCreateCurrency, useCurrenciesList } from './api';
import type { Currency } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /settings/currencies — system-wide reference catalog (Currency.cs doc). Not
 * server-paginated (the catalog is always small) — search/pagination happen client-side,
 * but the screen still follows the standard List/Edit DataGrid pattern. */
export function CurrenciesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: currencies, isLoading } = useCurrenciesList();
  const [showImport, setShowImport] = useState(false);
  const createCurrency = useCreateCurrency();

  const filtered = (currencies ?? []).filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return c.code.toLowerCase().includes(query) || c.nameAr.toLowerCase().includes(query) || c.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<Currency> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<Currency>[] = [
    { key: 'code', label: t('currencies.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('currencies.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: t('currencies.nameEn'), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'isDefault', label: t('currencies.isDefault'), render: (r) => (r.isDefault ? '★' : ''), exportValue: (r) => (r.isDefault ? t('common.yes') : t('common.no')) },
    { key: 'isActive', label: t('currencies.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];

      if (!code || !nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: Code/NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createCurrency.mutateAsync({ code, nameAr, nameEn });
        successCount++;
      } catch (error) {
        errors.push(`${code}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }

    return { successCount, errors };
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('currencies.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/settings/currencies/new')}>{t('currencies.addCurrency')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'USD' }, { header: 'NameAr', example: 'دولار أمريكي' }, { header: 'NameEn', example: 'US Dollar' }]}
          templateFileName={t('currencies.title')}
          onImportRows={handleImportRows}
        />
      )}

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/settings/currencies/${row.id}`)}
        exportFileName={t('currencies.title')}
      />
    </div>
  );
}
