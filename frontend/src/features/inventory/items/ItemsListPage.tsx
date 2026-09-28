import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useItemsList } from './api';
import type { ItemListItem } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

const itemTypeKey: Record<ItemListItem['itemType'], string> = {
  RawMaterial: 'items.typeRawMaterial',
  SemiFinished: 'items.typeSemiFinished',
  FinishedGood: 'items.typeFinishedGood',
  Consumable: 'items.typeConsumable',
  Service: 'items.typeService'
};

/** /inventory/items — main item catalog (02-Module-Inventory-Manufacturing.md, section 2.1). Not
 * server-paginated for now, matching every other reference screen built in this phase. */
export function ItemsListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: items, isLoading } = useItemsList();
  const { label } = useFieldLabels('INVENTORY_ITEMS');

  const filtered = (items ?? []).filter((i) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return i.code.toLowerCase().includes(query) || i.nameAr.toLowerCase().includes(query) || i.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<ItemListItem> = {
    items: filtered,
    totalCount: filtered.length,
    page: 1,
    pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<ItemListItem>[] = [
    { key: 'code', label: label('code', t('items.code')), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: label('nameAr', t('items.nameAr')), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: label('nameEn', t('items.nameEn')), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'itemType', label: label('itemType', t('items.itemType')), render: (r) => t(itemTypeKey[r.itemType]), exportValue: (r) => t(itemTypeKey[r.itemType]) },
    { key: 'baseUnitOfMeasureCode', label: label('baseUnitOfMeasureCode', t('items.baseUnit')), render: (r) => r.baseUnitOfMeasureCode, exportValue: (r) => r.baseUnitOfMeasureCode },
    { key: 'status', label: label('status', t('items.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => t(`status.${r.status}`) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('items.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/items/new')}>{t('items.addItem')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/inventory/items/${row.id}`)}
        exportFileName={t('items.title')}
      />
    </div>
  );
}
