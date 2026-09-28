import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useLoyaltyTiersList } from './api';
import type { LoyaltyTierListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /sales/loyalty-tiers — screen #4 (04-Module-Sales.md, section 5). */
export function LoyaltyTiersListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: tiers, isLoading } = useLoyaltyTiersList();
  const { label } = useFieldLabels('SALES_LOYALTY_TIERS');

  const filtered = (tiers ?? []).filter((r) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return r.code.toLowerCase().includes(query) || r.nameAr.toLowerCase().includes(query) || r.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<LoyaltyTierListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<LoyaltyTierListItem>[] = [
    { key: 'code', label: label('code', t('loyaltyTiers.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('loyaltyTiers.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('loyaltyTiers.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'displayOrder', label: label('displayOrder', t('loyaltyTiers.displayOrder')), render: (r) => r.displayOrder, exportValue: (r) => r.displayOrder },
    { key: 'minPointsThreshold', label: label('minPointsThreshold', t('loyaltyTiers.minPointsThreshold')), render: (r) => r.minPointsThreshold.toFixed(2), exportValue: (r) => r.minPointsThreshold },
    { key: 'earnRateMultiplier', label: label('earnRateMultiplier', t('loyaltyTiers.earnRateMultiplier')), render: (r) => r.earnRateMultiplier.toFixed(2), exportValue: (r) => r.earnRateMultiplier },
    { key: 'isActive', label: label('isActive', t('loyaltyTiers.isActive')), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('loyaltyTiers.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/sales/loyalty-tiers/new')}>{t('loyaltyTiers.addTier')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/sales/loyalty-tiers/${row.id}`)}
        exportFileName={t('loyaltyTiers.title')}
      />
    </div>
  );
}
