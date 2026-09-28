import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreatePOSCategory, usePOSCategoriesList } from './api';
import type { POSCategory } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/pos-categories — dedicated reference list (02-Module-Inventory-Manufacturing.md,
 * section 2.1). Not server-paginated (a company's POS-category count is always small). */
export function POSCategoriesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: categories, isLoading } = usePOSCategoriesList();
  const { label } = useFieldLabels('INVENTORY_POS_CATEGORIES');
  const [showImport, setShowImport] = useState(false);
  const createPOSCategory = useCreatePOSCategory();

  const filtered = (categories ?? []).filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return c.code.toLowerCase().includes(query) || c.nameAr.toLowerCase().includes(query) || c.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<POSCategory> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<POSCategory>[] = [
    { key: 'code', label: label('code', t('posCategories.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('posCategories.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('posCategories.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'displayOrder', label: label('displayOrder', t('posCategories.displayOrder')), render: (r) => r.displayOrder, exportValue: (r) => r.displayOrder },
    { key: 'isActive', label: label('isActive', t('posCategories.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const displayOrder = Number(row['DisplayOrder'] || row['displayOrder'] || 0);

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createPOSCategory.mutateAsync({ code: code || undefined, nameAr, nameEn, displayOrder });
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
        <h2 style={{ margin: 0 }}>{t('posCategories.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/pos-categories/new')}>{t('posCategories.addPOSCategory')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'CAT1' }, { header: 'NameAr', example: 'مشروبات' }, { header: 'NameEn', example: 'Beverages' }, { header: 'DisplayOrder', example: '1' }]}
          templateFileName={t('posCategories.title')}
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
        onRowClick={(row) => navigate(`/inventory/pos-categories/${row.id}`)}
        exportFileName={t('posCategories.title')}
      />
    </div>
  );
}
