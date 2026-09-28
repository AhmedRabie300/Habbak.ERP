import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useRFQsList } from './api';
import type { RFQListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/rfqs — screen #3 (03-Module-Purchasing.md, section 8), "مع مقارنة عروض الموردين". */
export function RFQsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useRFQsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_RFQ');

  const columns: DataGridColumn<RFQListItem>[] = [
    { key: 'rfqNumber', label: label('rfqNumber', t('rfqs.rfqNumber')), render: (r) => r.rfqNumber, exportValue: (r) => r.rfqNumber },
    { key: 'rfqDate', label: label('rfqDate', t('rfqs.rfqDate')), render: (r) => r.rfqDate, exportValue: (r) => r.rfqDate },
    { key: 'lineCount', label: label('lineCount', t('rfqs.lineCount')), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'supplierCount', label: label('supplierCount', t('rfqs.supplierCount')), render: (r) => r.supplierCount, exportValue: (r) => r.supplierCount },
    { key: 'status', label: label('status', t('rfqs.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('rfqs.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/rfqs/new')}>{t('rfqs.addRFQ')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/rfqs/${row.id}`)}
        exportFileName={t('rfqs.title')}
      />
    </div>
  );
}
