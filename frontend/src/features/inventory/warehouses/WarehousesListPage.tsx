import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { useCreateWarehouse, useWarehousesList } from './api';
import type { Warehouse, WarehouseType } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

const warehouseTypeKey: Record<Warehouse['warehouseType'], string> = {
  Main: 'warehouses.typeMain',
  BranchMaterials: 'warehouses.typeBranchMaterials',
  Production: 'warehouses.typeProduction',
  DamagedReturns: 'warehouses.typeDamagedReturns',
  FinishedGoods: 'warehouses.typeFinishedGoods'
};

/** /inventory/warehouses — dedicated reference list (02-Module-Inventory-Manufacturing.md,
 * section 2.1). Not server-paginated (a company's warehouse count is always small). */
export function WarehousesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: warehouses, isLoading } = useWarehousesList();
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('INVENTORY_WAREHOUSES');
  const [showImport, setShowImport] = useState(false);
  const createWarehouse = useCreateWarehouse();

  const filtered = (warehouses ?? []).filter((w) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return w.code.toLowerCase().includes(query) || w.nameAr.toLowerCase().includes(query) || w.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<Warehouse> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const branchName = (branchId: number | null) => (branches ?? []).find((b) => b.id === branchId)?.nameAr ?? '';

  const columns: DataGridColumn<Warehouse>[] = [
    { key: 'code', label: label('code', t('warehouses.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('warehouses.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('warehouses.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'warehouseType', label: label('warehouseType', t('warehouses.warehouseType')), render: (r) => t(warehouseTypeKey[r.warehouseType]), exportValue: (r) => t(warehouseTypeKey[r.warehouseType]) },
    { key: 'branchId', label: label('branchId', t('warehouses.branch')), render: (r) => branchName(r.branchId) || '—', exportValue: (r) => branchName(r.branchId) },
    { key: 'isActive', label: label('isActive', t('warehouses.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;
    const validTypes: WarehouseType[] = ['Main', 'BranchMaterials', 'Production', 'DamagedReturns', 'FinishedGoods'];
    const branchesByCode = new Map((branches ?? []).map((b) => [b.code, b.id]));

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const typeRaw = row['WarehouseType'] || row['warehouseType'];
      const warehouseType = validTypes.includes(typeRaw as WarehouseType) ? (typeRaw as WarehouseType) : 'Main';
      const branchCode = row['BranchCode'] || row['branchCode'];
      const branchId = branchCode ? (branchesByCode.get(branchCode) ?? null) : null;

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createWarehouse.mutateAsync({ code: code || undefined, nameAr, nameEn, warehouseType, branchId, allowNegativeBalance: false });
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
        <h2 style={{ margin: 0 }}>{t('warehouses.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/warehouses/new')}>{t('warehouses.addWarehouse')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'WH1' }, { header: 'NameAr', example: 'المخزن الرئيسي' }, { header: 'NameEn', example: 'Main Warehouse' }, { header: 'WarehouseType', example: 'Main' }, { header: 'BranchCode' }]}
          templateFileName={t('warehouses.title')}
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
        onRowClick={(row) => navigate(`/inventory/warehouses/${row.id}`)}
        exportFileName={t('warehouses.title')}
      />
    </div>
  );
}
