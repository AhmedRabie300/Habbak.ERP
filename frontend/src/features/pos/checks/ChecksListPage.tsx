import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useButtonChecker, useCurrentScreenCode } from '../../auth/access';
import { DataGrid, type DataGridColumn } from '../../../ui-kit/DataGrid';
import { Button } from '../../../ui-kit/Button';
import { StatusBadge } from '../../../ui-kit/Badge';
import { getFieldErrorMessage } from '../../../app/api';
import { useToastStore } from '../../../store/toastStore';
import { useHeldChecksList, useMergeChecks, useOpenChecksList } from './api';
import type { CheckListItem } from './types';
import type { PagedResult } from '../../../app/apiTypes';

/** /pos/checks/open و /pos/checks/held — screens #9 و#7 (05-Module-POS-Shifts.md). شاشة الشيكات
 * المفتوحة تفاعلية (Checkbox لدمج أكتر من شيك، قاعدة 9)؛ شاشة المعلّقة List قياسي بس. */
export function ChecksListPage({ kind }: { kind: 'open' | 'held' }) {
  const { t } = useTranslation();
  // Open and held checks are two screens sharing this page: the Merge button of whichever is open.
  const canMerge = useButtonChecker()(useCurrentScreenCode(), 'Merge');
  const navigate = useNavigate();
  const showToast = useToastStore((s) => s.show);
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  const openQuery = useOpenChecksList();
  const heldQuery = useHeldChecksList();
  const { data: checks, isLoading } = kind === 'open' ? openQuery : heldQuery;
  const mergeMutation = useMergeChecks();

  const filtered = (checks ?? []).filter((c) => {
    const query = search.trim().toLowerCase();
    if (!query) return true;
    return c.checkCode.toLowerCase().includes(query) || (c.tableCode ?? '').toLowerCase().includes(query);
  });

  const data: PagedResult<CheckListItem> = {
    items: filtered, totalCount: filtered.length, page: 1, pageSize: Math.max(filtered.length, 1)
  };

  const toggleSelect = (id: number) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  };

  const handleMerge = async () => {
    if (selectedIds.length < 2) return;
    const [target, ...sources] = selectedIds;
    try {
      await mergeMutation.mutateAsync({ targetCheckId: target, sourceCheckIds: sources });
      showToast(t('checksList.mergeSuccess'), 'success');
      setSelectedIds([]);
    } catch (error) {
      const message = getFieldErrorMessage(error);
      if (message) showToast(message, 'error');
    }
  };

  const columns: DataGridColumn<CheckListItem>[] = [
    ...(kind === 'open' ? [{
      key: 'select', label: '',
      render: (r: CheckListItem) => (
        <input type="checkbox" checked={selectedIds.includes(r.id)} onChange={(e) => { e.stopPropagation(); toggleSelect(r.id); }} onClick={(e) => e.stopPropagation()} />
      ),
      exportValue: () => ''
    } as DataGridColumn<CheckListItem>] : []),
    { key: 'checkCode', label: t('checksList.checkCode'), render: (r) => r.checkCode, exportValue: (r) => r.checkCode },
    { key: 'tableCode', label: t('checksList.table'), render: (r) => r.tableCode ?? '—', exportValue: (r) => r.tableCode ?? '' },
    { key: 'orderType', label: t('checksList.orderType'), render: (r) => t(`checkEdit.orderType${r.orderType}`), exportValue: (r) => r.orderType },
    { key: 'status', label: t('checksList.status'), render: (r) => <StatusBadge status={r.status} />, exportValue: (r) => r.status },
    { key: 'lineCount', label: t('checksList.lineCount'), render: (r) => r.lineCount, exportValue: (r) => r.lineCount },
    { key: 'total', label: t('checksList.total'), render: (r) => r.total.toFixed(2), exportValue: (r) => r.total },
    { key: 'openedAtUtc', label: t('checksList.openedAt'), render: (r) => new Date(r.openedAtUtc).toLocaleString(), exportValue: (r) => r.openedAtUtc }
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h2 style={{ margin: 0 }}>{kind === 'open' ? t('checksList.openTitle') : t('checksList.heldTitle')}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          {kind === 'open' && (
            <Button variant="primary" disabled={!canMerge || selectedIds.length < 2} onClick={handleMerge}>
              {t('checksList.mergeSelected')}
            </Button>
          )}
          <Button variant="secondary" onClick={() => navigate('/pos/table-board')}>{t('common.back')}</Button>
        </div>
      </div>

      <DataGrid
        columns={columns}
        data={data}
        isLoading={isLoading}
        search={search}
        onSearchChange={setSearch}
        page={1}
        onPageChange={() => {}}
        onRowClick={(row) => navigate(`/pos/checks/${row.id}`)}
        exportFileName={kind === 'open' ? t('checksList.openTitle') : t('checksList.heldTitle')}
      />
    </div>
  );
}
