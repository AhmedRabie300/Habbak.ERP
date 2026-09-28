import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSupplierContractsList } from './api';
import type { SupplierContractListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/supplier-contracts — screen #10 (03-Module-Purchasing.md, section 8). */
export function SupplierContractsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSupplierContractsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_CONTRACT');

  const columns: DataGridColumn<SupplierContractListItem>[] = [
    { key: 'contractNumber', label: label('contractNumber', t('supplierContracts.contractNumber')), render: (r) => r.contractNumber, exportValue: (r) => r.contractNumber },
    { key: 'supplier', label: label('supplier', t('supplierContracts.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'startDate', label: label('startDate', t('supplierContracts.startDate')), render: (r) => r.startDate, exportValue: (r) => r.startDate },
    { key: 'endDate', label: label('endDate', t('supplierContracts.endDate')), render: (r) => r.endDate, exportValue: (r) => r.endDate },
    { key: 'itemCount', label: label('itemCount', t('supplierContracts.itemCount')), render: (r) => r.itemCount, exportValue: (r) => r.itemCount },
    { key: 'status', label: label('status', t('supplierContracts.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('supplierContracts.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/supplier-contracts/new')}>{t('supplierContracts.addSupplierContract')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/supplier-contracts/${row.id}`)}
        exportFileName={t('supplierContracts.title')}
      />
    </div>
  );
}
