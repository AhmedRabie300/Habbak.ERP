import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { usePriceListsList } from './api';
import type { PriceListListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/price-lists — screen #2 (04-Module-Sales.md, section 5). */
export function PriceListsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: priceLists, isLoading } = usePriceListsList();
  const { label } = useFieldLabels('SALES_PRICE_LISTS');

  const filtered = (priceLists ?? []).filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return r.code.toLowerCase().includes(query) || r.nameAr.toLowerCase().includes(query) || r.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<PriceListListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<PriceListListItem>[] = [
    { key: 'code', label: label('code', t('priceLists.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('priceLists.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('priceLists.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'effectiveFromDate', label: label('effectiveFromDate', t('priceLists.effectiveFromDate')), render: (r) => r.effectiveFromDate, exportValue: (r) => r.effectiveFromDate },
    { key: 'branchCount', label: label('branchCount', t('priceLists.branchCount')), render: (r) => r.branchCount, exportValue: (r) => r.branchCount },
    { key: 'itemCount', label: label('itemCount', t('priceLists.itemCount')), render: (r) => r.itemCount, exportValue: (r) => r.itemCount },
    { key: 'isActive', label: label('isActive', t('priceLists.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('priceLists.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/price-lists/new')}>{t('priceLists.addPriceList')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/sales/price-lists/${row.id}`)}
        exportFileName={t('priceLists.title')}
      />
    </div>
  );
}
