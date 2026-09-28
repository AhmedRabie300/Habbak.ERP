import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePurchaseExpensesList } from './api';
import type { PurchaseExpenseListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/purchase-expenses — screen #8 (03-Module-Purchasing.md, section 8), "توزيع على
 * بنود الفاتورة" — itemizes each invoice's additional-costs breakdown. */
export function PurchaseExpensesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = usePurchaseExpensesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_PURCHASE_EXPENSES');

  const columns: DataGridColumn<PurchaseExpenseListItem>[] = [
    { key: 'invoiceNumber', label: label('invoiceNumber', t('purchaseExpenses.invoiceNumber')), render: (r) => r.invoiceNumber, exportValue: (r) => r.invoiceNumber },
    { key: 'supplier', label: label('supplier', t('purchaseExpenses.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'expenseType', label: label('expenseType', t('purchaseExpenses.expenseType')), render: (r) => t(`purchaseExpenses.type${r.expenseType}`), exportValue: (r) => r.expenseType },
    { key: 'amount', label: label('amount', t('purchaseExpenses.amount')), render: (r) => r.amount.toFixed(2), exportValue: (r) => r.amount },
    { key: 'allocationMethod', label: label('allocationMethod', t('purchaseExpenses.allocationMethod')), render: (r) => t(`purchaseInvoices.allocation${r.allocationMethod}`), exportValue: (r) => r.allocationMethod }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('purchaseExpenses.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/purchase-expenses/new')}>{t('purchaseExpenses.addPurchaseExpense')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/purchase-expenses/${row.id}`)}
        exportFileName={t('purchaseExpenses.title')}
      />
    </div>
  );
}
