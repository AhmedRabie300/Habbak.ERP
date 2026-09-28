import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useCustomersList } from './api';
import type { CustomerListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';
import { useFieldAccess } from '../../auth/access';

/** /sales/customers — screen #1 (04-Module-Sales.md, section 5). Not server-paginated, same
 * reasoning as Suppliers — reference data picked from dropdowns throughout the rest of the module
 * (quotes, orders, invoices). */
export function CustomersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: customers, isLoading } = useCustomersList();
  const { label } = useFieldLabels('SALES_CUSTOMERS');

  const filtered = (customers ?? []).filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return c.code.toLowerCase().includes(query) || c.nameAr.toLowerCase().includes(query) || c.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<CustomerListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const canViewCredit = useFieldAccess('SALES_CUSTOMERS', 'Customer')('CreditLimit').canView;

  const columns: DataGridColumn<CustomerListItem>[] = [
    { key: 'code', label: label('code', t('customers.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('customers.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('customers.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'customerType', label: label('customerType', t('customers.customerType')), render: (r) => t(`customers.type${r.customerType}`), exportValue: (r) => r.customerType },
    ...(canViewCredit
      ? [{ key: 'creditLimit', label: label('creditLimit', t('customers.creditLimit')), render: (r: CustomerListItem) => r.creditLimit?.toFixed(2) ?? '', exportValue: (r: CustomerListItem) => r.creditLimit ?? '' }]
      : []),
    { key: 'loyaltyPointsBalance', label: label('loyaltyPointsBalance', t('customers.loyaltyPointsBalance')), render: (r) => r.loyaltyPointsBalance.toFixed(2), exportValue: (r) => r.loyaltyPointsBalance },
    { key: 'isActive', label: label('isActive', t('customers.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('customers.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/customers/new')}>{t('customers.addCustomer')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/sales/customers/${row.id}`)}
        exportFileName={t('customers.title')}
      />
    </div>
  );
}
