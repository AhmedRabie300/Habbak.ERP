import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSupplierPaymentsList } from './api';
import type { SupplierPaymentListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/supplier-payments — screen #9 (03-Module-Purchasing.md, section 8). Thin Purchasing
 * view over the Voucher screen (VoucherType.Payment, CounterpartyType.Supplier). */
export function SupplierPaymentsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSupplierPaymentsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_PAYMENTS');

  const columns: DataGridColumn<SupplierPaymentListItem>[] = [
    { key: 'voucherNumber', label: label('voucherNumber', t('supplierPayments.voucherNumber')), render: (r) => r.voucherNumber, exportValue: (r) => r.voucherNumber },
    { key: 'voucherDate', label: label('voucherDate', t('supplierPayments.voucherDate')), render: (r) => r.voucherDate, exportValue: (r) => r.voucherDate },
    { key: 'description', label: label('description', t('supplierPayments.description')), render: (r) => r.description ?? '—', exportValue: (r) => r.description ?? '' },
    { key: 'amount', label: label('amount', t('supplierPayments.amount')), render: (r) => r.amount.toFixed(2), exportValue: (r) => r.amount },
    { key: 'status', label: label('status', t('supplierPayments.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('supplierPayments.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/supplier-payments/new')}>{t('supplierPayments.addSupplierPayment')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/supplier-payments/${row.id}`)}
        exportFileName={t('supplierPayments.title')}
      />
    </div>
  );
}
