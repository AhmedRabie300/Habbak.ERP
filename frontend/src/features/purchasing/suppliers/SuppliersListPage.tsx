import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreateSupplier, useSuppliersList } from './api';
import type { SupplierListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/suppliers — screen #1 (03-Module-Purchasing.md, section 8). Not server-paginated,
 * same reasoning as Warehouses/Items/CustodyOfficers — reference data picked from dropdowns
 * throughout the rest of the module. */
export function SuppliersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: suppliers, isLoading } = useSuppliersList();
  const { label } = useFieldLabels('PURCHASING_SUPPLIERS');
  const [showImport, setShowImport] = useState(false);
  const createSupplier = useCreateSupplier();

  const filtered = (suppliers ?? []).filter((s) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return s.code.toLowerCase().includes(query) || s.nameAr.toLowerCase().includes(query) || s.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<SupplierListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<SupplierListItem>[] = [
    { key: 'code', label: label('code', t('suppliers.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('suppliers.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('suppliers.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'paymentTerms', label: label('paymentTerms', t('suppliers.paymentTerms')), render: (r) => t(`suppliers.terms${r.paymentTerms}`), exportValue: (r) => r.paymentTerms },
    { key: 'currencyCode', label: label('currencyCode', t('suppliers.currency')), render: (r) => r.currencyCode, exportValue: (r) => r.currencyCode },
    { key: 'isActive', label: label('isActive', t('suppliers.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  const handleImportRows = async (rows: Record<string, string>[]): Promise<ImportResult> => {
    const errors: string[] = [];
    let successCount = 0;
    const validTerms = ['Cash', 'Net15', 'Net30', 'Net60'];

    for (const [index, row] of rows.entries()) {
      const nameAr = row['NameAr'] || row['nameAr'];
      const nameEn = row['NameEn'] || row['nameEn'];
      const code = row['Code'] || row['code'];
      const paymentTermsRaw = row['PaymentTerms'] || row['paymentTerms'];
      const paymentTerms = validTerms.includes(paymentTermsRaw) ? paymentTermsRaw : 'Cash';
      const currencyCode = row['CurrencyCode'] || row['currencyCode'] || 'EGP';

      if (!nameAr || !nameEn) {
        errors.push(`${t('common.rowsFound')} ${index + 1}: NameAr/NameEn ${t('common.noData')}`);
        continue;
      }

      try {
        await createSupplier.mutateAsync({ code: code || undefined, nameAr, nameEn, paymentTerms, currencyCode, isActive: true });
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
        <h2 style={{ margin: 0 }}>{t('suppliers.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/suppliers/new')}>{t('suppliers.addSupplier')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'SUP1' }, { header: 'NameAr', example: 'مورد تجريبي' }, { header: 'NameEn', example: 'Sample Supplier' }, { header: 'PaymentTerms', example: 'Cash' }, { header: 'CurrencyCode', example: 'EGP' }]}
          templateFileName={t('suppliers.title')}
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
        onRowClick={(row) => navigate(`/purchasing/suppliers/${row.id}`)}
        exportFileName={t('suppliers.title')}
      />
    </div>
  );
}
