import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreateItemGroup, useItemGroupsList } from './api';
import type { ItemGroup } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/item-groups — dedicated reference list (02-Module-Inventory-Manufacturing.md,
 * section 2.1). Not server-paginated (a company's item-group count is always small). */
export function ItemGroupsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: groups, isLoading } = useItemGroupsList();
  const { label } = useFieldLabels('INVENTORY_ITEM_GROUPS');
  const [showImport, setShowImport] = useState(false);
  const createItemGroup = useCreateItemGroup();

  const filtered = (groups ?? []).filter((g) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return g.code.toLowerCase().includes(query) || g.nameAr.toLowerCase().includes(query) || g.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<ItemGroup> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const parentName = (parentId: number | null) => (groups ?? []).find((g) => g.id === parentId)?.nameAr ?? '';

  const columns: DataGridColumn<ItemGroup>[] = [
    { key: 'code', label: label('code', t('itemGroups.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('itemGroups.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('itemGroups.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'parentId', label: label('parentId', t('itemGroups.parent')), render: (r) => parentName(r.parentId) || '—', exportValue: (r) => parentName(r.parentId) },
    { key: 'isActive', label: label('isActive', t('itemGroups.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;
    const groupsByCode = new Map((groups ?? []).map((g) => [g.code, g.id]));

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const parentCode = row['ParentCode'] || row['parentCode'];
      const parentId = parentCode ? (groupsByCode.get(parentCode) ?? null) : null;

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        const { id } = await createItemGroup.mutateAsync({ code: code || undefined, nameAr, nameEn, parentId });
        groupsByCode.set(code || nameAr, id);
        successCount++;
      } catch (error) {
        errors.push(`${row['Code'] || row['code'] || nameAr}: ${error instanceof Error ? error.message : String(error)}`);
      }
    }

    return { successCount, errors };
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('itemGroups.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/item-groups/new')}>{t('itemGroups.addItemGroup')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'GRP1' }, { header: 'NameAr', example: 'مجموعة تجريبية' }, { header: 'NameEn', example: 'Sample Group' }, { header: 'ParentCode' }]}
          templateFileName={t('itemGroups.title')}
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
        onRowClick={(row) => navigate(`/inventory/item-groups/${row.id}`)}
        exportFileName={t('itemGroups.title')}
      />
    </div>
  );
}
