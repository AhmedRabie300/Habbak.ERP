import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useDimensionsList } from './api';
import type { Dimension } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /accounting/dimensions — My Remarks/Remarks2.md, remark 2.1: the standard List/Edit pair,
 * replacing the old single dual-panel screen. Not server-paginated (a company's cost-center
 * count is always small), same convention as ItemGroupsListPage. */
export function DimensionsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: dimensions, isLoading } = useDimensionsList();
  const { label } = useFieldLabels('ACCOUNTING_DIMENSIONS');

  const filtered = (dimensions ?? []).filter((d) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return d.code.toLowerCase().includes(query) || d.nameAr.toLowerCase().includes(query) || d.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<Dimension> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<Dimension>[] = [
    { key: 'code', label: label('code', t('dimensions.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('dimensions.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('dimensions.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    {
      key: 'linkedEntityType',
      label: label('linkedEntityType', t('dimensions.linkedEntityType')),
      render: (r) => t(`dimensions.linkedType.${r.linkedEntityType}`),
      exportValue: (r) => t(`dimensions.linkedType.${r.linkedEntityType}`)
    },
    { key: 'isActive', label: t('dimensions.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('dimensions.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/accounting/dimensions/new')}>{t('dimensions.addDimension')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/accounting/dimensions/${row.id}`)}
        exportFileName={t('dimensions.title')}
      />
    </div>
  );
}
