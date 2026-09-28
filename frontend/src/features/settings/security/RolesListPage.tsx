import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { Badge } from '../../../ui-kit/Badge';
import { usePermission } from '../../../ui-kit/usePermission';
import type { PagedResult } from '../../../app/apiTypes';
import { useRolesList, type RoleListItem } from './api';
import { Hint, PageHeader } from './shared';

/** /settings/roles — the company's roles and how much each can do. */
export function RolesListPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const { data: roles, isLoading } = useRolesList();

  const q = search.trim().toLowerCase();
  const filtered = (roles ?? []).filter((r) => !q || r.code.toLowerCase().includes(q) || r.nameAr.includes(q) || r.nameEn.toLowerCase().includes(q));
  const data: PagedResult<RoleListItem> = { items: filtered, totalCount: filtered.length, page: 1, pageSize: Math.max(filtered.length, 1) };
  const name = (r: RoleListItem) => (i18n.language === 'en' ? r.nameEn : r.nameAr);

  const columns: DataGridColumn<RoleListItem>[] = [
    { key: 'code', label: t('security.code'), render: (r) => <span dir="ltr">{r.code}</span>, exportValue: (r) => r.code },
    { key: 'name', label: t('security.name'), render: (r) => name(r), exportValue: (r) => name(r) },
    {
      key: 'kind', label: t('security.kind'),
      render: (r) => (
        <span style={{ display: 'inline-flex', gap: 6 }}>
          {r.isSystemRole && <Badge label={t('security.systemRole')} tone="info" />}
          {r.isFullAccess && <Badge label={t('security.fullAccess')} tone="success" />}
          {!r.isActive && <Badge label={t('security.inactive')} tone="neutral" />}
        </span>
      ),
      exportValue: (r) => (r.isSystemRole ? t('security.systemRole') : '')
    },
    { key: 'screens', label: t('security.screenCount'), render: (r) => (r.isFullAccess ? t('security.all') : r.screenCount), exportValue: (r) => r.screenCount },
    { key: 'users', label: t('security.userCount'), render: (r) => r.userCount, exportValue: (r) => r.userCount }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader title={t('security.rolesTitle')}>
        {canAdd && <Button variant="primary" onClick={() => navigate('/settings/roles/new')}>{t('security.addRoleButton')}</Button>}
      </PageHeader>
      <Hint>{t('security.rolesListHint')}</Hint>
      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/settings/roles/${row.id}`)}
        exportFileName={t('security.rolesTitle')}
      />
    </div>
  );
}
