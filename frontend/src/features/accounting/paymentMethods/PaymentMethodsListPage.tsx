import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { ImportPanel, type ImportResult } from '../../../ui-kit/ImportPanel';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCreatePaymentMethod, usePaymentMethodsList } from './api';
import type { PaymentMethod } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/payment-methods — dedicated reference list (00-System-Wide-Corrections-02.md,
 * section 2.2). Not server-paginated (a company's payment-method count is always small) —
 * search/pagination happen client-side, but the screen still follows the standard List/Edit
 * DataGrid pattern. */
export function PaymentMethodsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: paymentMethods, isLoading } = usePaymentMethodsList();
  const { label } = useFieldLabels('ACCOUNTING_PAYMENT_METHODS');
  const [showImport, setShowImport] = useState(false);
  const createPaymentMethod = useCreatePaymentMethod();

  const filtered = (paymentMethods ?? []).filter((p) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return p.code.toLowerCase().includes(query) || p.nameAr.toLowerCase().includes(query) || p.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<PaymentMethod> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<PaymentMethod>[] = [
    { key: 'code', label: label('code', t('paymentMethods.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('paymentMethods.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('paymentMethods.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'isActive', label: label('isActive', t('paymentMethods.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
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
        await createPaymentMethod.mutateAsync({ code: code || undefined, nameAr, nameEn });
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
        <h2 style={{ margin: 0 }}>{t('paymentMethods.title')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="secondary" onClick={() => setShowImport((v) => !v)}>{t('common.importFromExcel')}</Button>
          {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/payment-methods/new')}>{t('paymentMethods.addPaymentMethod')}</Button>}
        </div>
      </div>

      {showImport && (
        <ImportPanel
          columns={[{ header: 'Code', example: 'PM1' }, { header: 'NameAr', example: 'نقدي' }, { header: 'NameEn', example: 'Cash' }]}
          templateFileName={t('paymentMethods.title')}
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
        onRowClick={(row) => navigate(`/accounting/payment-methods/${row.id}`)}
        exportFileName={t('paymentMethods.title')}
      />
    </div>
  );
}
