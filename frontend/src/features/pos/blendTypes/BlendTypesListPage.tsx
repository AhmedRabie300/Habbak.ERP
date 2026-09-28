import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { useBlendTypesList } from './api';
import type { BlendType } from './api';
import type { PagedResult } from '../../../app/apiTypes';
import { usePermission } from '../../../ui-kit/usePermission';

/** /pos/blend-types — مراجعة 2026-09-13، بند 2.1: قائمة مخصّصة لأنواع البن المتاحة للخلط، تُدار
 * هنا وتُستخدَم من شاشة استشاري التصنيع (/pos/blend-consultation). قائمة صغيرة، مفيش داعي لصفحات. */
export function BlendTypesListPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: blendTypes, isLoading } = useBlendTypesList();

  const filtered = (blendTypes ?? []).filter((b) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return b.code.toLowerCase().includes(query) || b.nameAr.toLowerCase().includes(query) || b.nameEn.toLowerCase().includes(query);
  });

  const data: PagedResult<BlendType> = {
    items: filtered, totalCount: filtered.length, page: 1, pageSize: Math.max(filtered.length, 1)
  };

  const columns: DataGridColumn<BlendType>[] = [
    { key: 'code', label: t('blendTypes.code'), render: (r) => r.code, exportValue: (r) => r.code },
    { key: 'nameAr', label: t('blendTypes.nameAr'), render: (r) => r.nameAr, exportValue: (r) => r.nameAr },
    { key: 'nameEn', label: t('blendTypes.nameEn'), render: (r) => r.nameEn, exportValue: (r) => r.nameEn },
    { key: 'pricePerGram', label: t('blendTypes.pricePerGram'), render: (r) => r.pricePerGram.toFixed(2), exportValue: (r) => r.pricePerGram },
    { key: 'isActive', label: t('blendTypes.isActive'), render: (r) => (r.isActive ? t('common.yes') : t('common.no')), exportValue: (r) => (r.isActive ? t('common.yes') : t('common.no')) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{t('blendTypes.title')}</h2>
        {canAdd && <Button variant="primary" onClick={() => navigate('/pos/blend-types/new')}>{t('blendTypes.addBlendType')}</Button>}
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/blend-types/${row.id}`)}
        exportFileName={t('blendTypes.title')}
      />
    </div>
  );
}
