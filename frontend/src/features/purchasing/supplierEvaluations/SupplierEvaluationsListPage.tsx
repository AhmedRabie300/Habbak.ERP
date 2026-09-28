import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useSupplierEvaluationsList } from './api';
import type { SupplierEvaluationListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /purchasing/supplier-evaluations — screen #12 (03-Module-Purchasing.md, section 8). */
export function SupplierEvaluationsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useSupplierEvaluationsList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('PURCHASING_SUPPLIER_EVALUATION');

  const columns: DataGridColumn<SupplierEvaluationListItem>[] = [
    { key: 'supplier', label: label('supplier', t('supplierEvaluations.supplier')), render: (r) => `${r.supplierCode} — ${r.supplierNameAr}`, exportValue: (r) => r.supplierCode },
    { key: 'evaluationDate', label: label('evaluationDate', t('supplierEvaluations.evaluationDate')), render: (r) => r.evaluationDate, exportValue: (r) => r.evaluationDate },
    {
      key: 'overallScore', label: label('overallScore', t('supplierEvaluations.overallScore')),
      render: (r) => (
        <span style={{ fontWeight: 700, color: r.overallScore >= 80 ? 'var(--color-success)' : r.overallScore >= 60 ? 'var(--color-warning)' : 'var(--color-error)' }}>
          {r.overallScore.toFixed(1)}
        </span>
      ),
      exportValue: (r) => r.overallScore
    }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('supplierEvaluations.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/purchasing/supplier-evaluations/new')}>{t('supplierEvaluations.addSupplierEvaluation')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/purchasing/supplier-evaluations/${row.id}`)}
        exportFileName={t('supplierEvaluations.title')}
      />
    </div>
  );
}
