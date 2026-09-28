import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { SearchableSelect } from '../../../ui-kit/SearchableSelect';
import { usePermission } from '../../../ui-kit/usePermission';
import type { PagedResult } from '../../../app/apiTypes';
import { useUsersList, type UserListItem, type UserStatus } from './api';
import { formatDateTime, PageHeader, UserStatusBadge } from './shared';

const STATUSES: UserStatus[] = ['Active', 'Locked', 'Suspended', 'PendingActivation'];

/** /settings/users — the people who can sign in to this company. */
export function UsersListPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const canAdd = usePermission('add');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<UserStatus | ''>('');
  const { data: users, isLoading } = useUsersList(search.trim(), status);

  const data: PagedResult<UserListItem> | undefined = users && { items: users, totalCount: users.length, page: 1, pageSize: Math.max(users.length, 1) };

  const columns: DataGridColumn<UserListItem>[] = [
    { key: 'username', label: t('security.username'), render: (r) => <span dir="ltr">{r.username}</span>, exportValue: (r) => r.username },
    { key: 'fullName', label: t('security.fullName'), render: (r) => r.fullName, exportValue: (r) => r.fullName },
    { key: 'email', label: t('security.email'), render: (r) => <span dir="ltr">{r.email}</span>, exportValue: (r) => r.email },
    { key: 'roles', label: t('security.roles'), render: (r) => r.roleCodes.map((c) => t(`security.roleNames.${c}`, { defaultValue: c })).join('، ') || '—', exportValue: (r) => r.roleCodes.join(', ') },
    { key: 'status', label: t('security.statusLabel'), render: (r) => <UserStatusBadge status={r.status} />, exportValue: (r) => t(`security.status.${r.status}`) },
    { key: 'lastLogin', label: t('security.lastLogin'), render: (r) => formatDateTime(r.lastLoginAtUtc, i18n.language), exportValue: (r) => formatDateTime(r.lastLoginAtUtc, i18n.language) }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader title={t('security.usersTitle')}>
        <SearchableSelect
          id="users-status"
          value={status}
          onChange={(v) => setStatus(v as UserStatus | '')}
          options={[{ value: '', label: t('security.allStatuses') }, ...STATUSES.map((s) => ({ value: s, label: t(`security.status.${s}`) }))]}
          style={{ minWidth: 170 }}
        />
        {canAdd && <Button variant="primary" onClick={() => navigate('/settings/users/new')}>{t('security.addUser')}</Button>}
      </PageHeader>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/settings/users/${row.id}`)}
        exportFileName={t('security.usersTitle')}
      />
    </div>
  );
}
