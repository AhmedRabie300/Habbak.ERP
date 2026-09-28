import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useBranchesList } from '../../organization/branches/api';
import { useCreateCustodyOfficer, useCustodyOfficersList } from './api';
import type { CustodyOfficer } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/custody-officers — screen #6 (02-Module-Inventory-Manufacturing.md, section 5).
 * Not server-paginated (a company's custody-officer count is always small). */
export function CustodyOfficersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: officers, isLoading } = useCustodyOfficersList();
  const { data: branches } = useBranchesList();
  const { label } = useFieldLabels('INVENTORY_CUSTODY_OFFICERS');
  const [showImport, setShowImport] = useState(false);
  const createCustodyOfficer = useCreateCustodyOfficer();

  const filtered = (officers ?? []).filter((o) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return o.code.toLowerCase().includes(query) || o.nameAr.toLowerCase().includes(query) || o.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<CustodyOfficer> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const branchName = (branchId: number | null) => (branches ?? []).find((b) => b.id === branchId)?.nameAr ?? '';

  const columns: DataGridColumn<CustodyOfficer>[] = [
    { key: 'code', label: label('code', t('custodyOfficers.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('custodyOfficers.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('custodyOfficers.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'branchId', label: label('branch', t('custodyOfficers.branch')), render: (r) => branchName(r.branchId) || '—', exportValue: (r) => branchName(r.branchId) },
    { key: 'isActive', label: label('isActive', t('custodyOfficers.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;
    const branchesByCode = new Map((branches ?? []).map((b) => [b.code, b.id]));

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const branchCode = row['BranchCode'] || row['branchCode'];
      const branchId = branchCode ? (branchesByCode.get(branchCode) ?? null) : null;

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createCustodyOfficer.mutateAsync({ code: code || undefined, nameAr, nameEn, branchId });
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
        <h2 style={{ margin: 0 }}>{t('custodyOfficers.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/custody-officers/new')}>{t('custodyOfficers.addCustodyOfficer')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'CO1' }, { header: 'NameAr', example: 'مسؤول عهدة تجريبي' }, { header: 'NameEn', example: 'Sample Officer' }, { header: 'BranchCode' }]}
          templateFileName={t('custodyOfficers.title')}
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
        onRowClick={(row) => navigate(`/inventory/custody-officers/${row.id}`)}
        exportFileName={t('custodyOfficers.title')}
      />
    </div>
  );
}
