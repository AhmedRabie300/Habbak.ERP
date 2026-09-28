import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList, useCreateBranch } from './api';
import type { Branch } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/branches — company-wide master data; a Branch-linked cost center type's values
 * mirror this screen's own active records (see DimensionsPage). Not server-paginated (a
 * company's branch count is always small) — search/pagination happen client-side against the
 * one full list, but the screen still follows the standard List/Edit DataGrid pattern. */
export function BranchesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: branches, isLoading } = useBranchesList();
  const { label } = useFieldLabels('ORG_BRANCHES');
  const [showImport, setShowImport] = useState(false);
  const createBranch = useCreateBranch();

  const filtered = (branches ?? []).filter((b) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return b.code.toLowerCase().includes(query) || b.nameAr.toLowerCase().includes(query) || b.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<Branch> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<Branch>[] = [
    { key: 'code', label: label('code', t('branches.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('branches.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('branches.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'isActive', label: label('isActive', t('branches.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createBranch.mutateAsync({ code: code || undefined, nameAr, nameEn });
        successCount++;
      } catch (error) {
        errors.push(`${code || nameAr}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }

    return { successCount, errors };
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('branches.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/branches/new')}>{t('branches.addBranch')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'BR1' }, { header: 'NameAr', example: 'فرع تجريبي' }, { header: 'NameEn', example: 'Sample Branch' }]}
          templateFileName={t('branches.title')}
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
        onRowClick={(row) => navigate(`/accounting/branches/${row.id}`)}
        exportFileName={t('branches.title')}
      />
    </div>
  );
}
