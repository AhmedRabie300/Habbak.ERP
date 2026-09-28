import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useVouchersList } from './api';
import type { VoucherKind, VoucherListItem } from './types';

/** Shared List screen for /accounting/receipt-vouchers and /accounting/payment-vouchers. */
export function VouchersListPage({ kind }: { kind: VoucherKind }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useVouchersList(kind, { search, page, pageSize: 25 });
  const isReceipt = kind === 'receipt-vouchers';
  const title = isReceipt ? t('vouchers.receiptTitle') : t('vouchers.paymentTitle');
  const addLabel = isReceipt ? t('vouchers.newReceipt') : t('vouchers.newPayment');
  const { label } = useFieldLabels(isReceipt ? 'ACCOUNTING_RECEIPT_VOUCHERS' : 'ACCOUNTING_PAYMENT_VOUCHERS');

  const columns: DataGridColumn<VoucherListItem>[] = [
    { key: 'voucherNumber', label: label('voucherNumber', t('vouchers.voucherNumber')), render: (r) => r.voucherNumber, exportValue: (r) => r.voucherNumber },
    { key: 'voucherDate', label: label('date', t('vouchers.date')), render: (r) => r.voucherDate, exportValue: (r) => r.voucherDate },
    { key: 'counterpartyType', label: label('counterpartyType', t('vouchers.counterpartyType')), render: (r) => r.counterpartyType, exportValue: (r) => r.counterpartyType },
    { key: 'amount', label: label('amount', t('vouchers.amount')), render: (r) => r.amount.toLocaleString(), exportValue: (r) => r.amount },
    { key: 'status', label: label('status', t('vouchers.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{title}</h2>
        <Button variant="primary" onClick={() => navigate(`/accounting/${kind}/new`)}>{addLabel}</Button>
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/accounting/${kind}/${row.id}`)}
        exportFileName={title}
      />
    </div>
  );
}
