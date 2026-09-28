import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { usePermission } from '../../../ui-kit/usePermission';
import { useJobPositions } from '../jobPositions/api';
import { useEmployeesList } from './api';
import type { EmployeeListItem } from './types';

/** /hr/employees — screen HR_EMPLOYEES. Server-paginated (unlike the plain lookups in 1.5.2) —
 * a company's employee roster can be large, matching JournalEntriesListPage's pattern. */
export function EmployeesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useEmployeesList({ search, page, pageSize: 25 });
  const { data: jobPositions } = useJobPositions();
  const positionName = (id: number) => jobPositions?.find((p) => p.id === id)?.nameAr ?? '—';

  const columns: DataGridColumn<EmployeeListItem>[] = [
    { key: 'code', label: t('hr.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('hr.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'jobPosition', label: t('hr.jobPositions.title'), render: (r) => positionName(r.jobPositionId), exportValue: (r) => positionName(r.jobPositionId) },
    { key: 'hireDate', label: t('hr.employees.hireDate'), render: (r) => r.hireDate, exportValue: (r) => r.hireDate },
    { key: 'status', label: t('hr.employees.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('hr.employees.title')}</h2>
        {canAdd && (
          <Button variant="primary" onClick={() => navigate('/hr/employees/new')}>
            {t('hr.employees.add')}
          </Button>
        )}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/hr/employees/${row.id}`)}
        exportFileName={t('hr.employees.title')}
      />
    </div>
  );
}
