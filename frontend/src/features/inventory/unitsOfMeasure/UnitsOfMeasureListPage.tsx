import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreateUnitOfMeasure, useUnitsOfMeasureList } from './api';
import type { UnitCategory, UnitOfMeasure } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

const categoryKey: Record<UnitOfMeasure['category'], string> = {
  Weight: 'unitsOfMeasure.categoryWeight',
  Volume: 'unitsOfMeasure.categoryVolume',
  Count: 'unitsOfMeasure.categoryCount'
};

/** /inventory/units-of-measure — dedicated reference list (02-Module-Inventory-Manufacturing.md,
 * section 2.1). Not server-paginated (a company's unit-of-measure count is always small). */
export function UnitsOfMeasureListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: units, isLoading } = useUnitsOfMeasureList();
  const { label } = useFieldLabels('INVENTORY_UNITS_OF_MEASURE');
  const [showImport, setShowImport] = useState(false);
  const createUnitOfMeasure = useCreateUnitOfMeasure();

  const filtered = (units ?? []).filter((u) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return u.code.toLowerCase().includes(query) || u.nameAr.toLowerCase().includes(query) || u.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<UnitOfMeasure> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<UnitOfMeasure>[] = [
    { key: 'code', label: label('code', t('unitsOfMeasure.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('unitsOfMeasure.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('unitsOfMeasure.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'category', label: label('category', t('unitsOfMeasure.category')), render: (r) => t(categoryKey[r.category]), exportValue: (r) => t(categoryKey[r.category]) },
    { key: 'isActive', label: label('isActive', t('unitsOfMeasure.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;
    const validCategories: UnitCategory[] = ['Weight', 'Volume', 'Count'];

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const categoryRaw = row['Category'] || row['category'];
      const category = validCategories.includes(categoryRaw as UnitCategory) ? (categoryRaw as UnitCategory) : 'Count';

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createUnitOfMeasure.mutateAsync({ code: code || undefined, nameAr, nameEn, category });
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
        <h2 style={{ margin: 0 }}>{t('unitsOfMeasure.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/units-of-measure/new')}>{t('unitsOfMeasure.addUnitOfMeasure')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'KG' }, { header: 'NameAr', example: 'كيلوجرام' }, { header: 'NameEn', example: 'Kilogram' }, { header: 'Category', example: 'Weight' }]}
          templateFileName={t('unitsOfMeasure.title')}
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
        onRowClick={(row) => navigate(`/inventory/units-of-measure/${row.id}`)}
        exportFileName={t('unitsOfMeasure.title')}
      />
    </div>
  );
}
