import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useDiscountsList } from './api';
import type { DiscountListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/discounts — screen #3 (04-Module-Sales.md, section 5). */
export function DiscountsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: discounts, isLoading } = useDiscountsList();
  const { label } = useFieldLabels('SALES_DISCOUNTS');

  const filtered = (discounts ?? []).filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return r.code.toLowerCase().includes(query) || r.nameAr.toLowerCase().includes(query) || r.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<DiscountListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<DiscountListItem>[] = [
    { key: 'code', label: label('code', t('discounts.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('discounts.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('discounts.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'discountType', label: label('discountType', t('discounts.discountType')), render: (r) => t(`discounts.type${r.discountType}`), exportValue: (r) => r.discountType },
    { key: 'value', label: label('value', t('discounts.value')), render: (r) => r.value.toFixed(2), exportValue: (r) => r.value },
    { key: 'applicationPriority', label: label('applicationPriority', t('discounts.applicationPriority')), render: (r) => r.applicationPriority, exportValue: (r) => r.applicationPriority },
    { key: 'isStackable', label: label('isStackable', t('discounts.isStackable')), render: (r) => (r.isStackable ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isStackable ? t('common.yes') : t('common.no')) },
    { key: 'isHappyHour', label: label('isHappyHour', t('discounts.isHappyHour')), render: (r) => (r.isHappyHour ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isHappyHour ? t('common.yes') : t('common.no')) },
    { key: 'isActive', label: label('isActive', t('discounts.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('discounts.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/discounts/new')}>{t('discounts.addDiscount')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/sales/discounts/${row.id}`)}
        exportFileName={t('discounts.title')}
      />
    </div>
  );
}
