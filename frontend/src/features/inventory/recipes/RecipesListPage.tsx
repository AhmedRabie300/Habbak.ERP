import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { useFieldLabels } from '../../common/useFieldLabels';
import { useRecipesList } from './api';
import type { RecipeListItem } from './types';
import { usePermission } from '../../../ui-kit/usePermission';

/** /inventory/recipes — screen #16 (02-Module-Inventory-Manufacturing.md, section 5). Only current
 * versions are listed (backend scoping) — a recipe's approval flow (screen #17) is folded into the
 * same edit page rather than a separate route, since approving needs no extra inputs beyond the
 * already-visible components/cost breakdown plus an Approve/Reject action. */
export function RecipesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useRecipesList({ search, page, pageSize: 25 });
  const { label } = useFieldLabels('INVENTORY_RECIPE');

  const columns: DataGridColumn<RecipeListItem>[] = [
    { key: 'recipeFamilyCode', label: label('recipeFamilyCode', t('recipes.familyCode')), render: (r) => `${r.recipeFamilyCode} (v${r.versionNumber})`, exportValue: (r) => `${r.recipeFamilyCode} v${r.versionNumber}` },
    { key: 'outputItem', label: label('outputItem', t('recipes.outputItem')), render: (r) => `${r.outputItemCode} — ${r.outputItemNameAr}`, exportValue: (r) => r.outputItemCode },
    { key: 'outputQuantity', label: label('outputQuantity', t('recipes.outputQuantity')), render: (r) => r.outputQuantity, exportValue: (r) => r.outputQuantity },
    { key: 'status', label: label('status', t('recipes.status')), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('recipes.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/inventory/recipes/new')}>{t('recipes.addRecipe')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={(v) => { setSearch(v); setPage(1); }}
        page={page}
        onPageChange={setPage}
        onRowClick={(row) => navigate(`/inventory/recipes/${row.id}`)}
        exportFileName={t('recipes.title')}
      />
    </div>
  );
}
