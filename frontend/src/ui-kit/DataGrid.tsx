import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Input } from './Field';
import { SkeletonRows } from './Skeleton';
import { Button } from './Button';
import { ExportMenu } from './ExportMenu';
import { Icon } from './Icon';
import { printTable, type ExportColumn } from '../lib/export';
import { useScreenRights } from '../features/auth/access';
import type { PagedResult } from '../app/apiTypes';

export interface DataGridColumn<T> {
  key: string;
  label: string;
  render: (row: T) => ReactNode;
  /** Plain value for Export/Import — required only when export is enabled for this grid. */
  exportValue?: (row: T) => string | number;
}

interface DataGridProps<T> {
  columns: DataGridColumn<T>[];
  data: PagedResult<T> | undefined;
  isLoading: boolean;
  search: string;
  onSearchChange: (value: string) => void;
  page: number;
  onPageChange: (page: number) => void;
  onRowClick?: (row: T) => void;
  /** When set (with columns' exportValue defined), shows the standard Export menu (section 8.5). */
  exportFileName?: string;
}

/**
 * The standard Data Grid (00-Frontend-Specs.md, section 4): Skeleton while loading, server-side
 * search/pagination always, row click navigates via the Router (never a Modal — section 5).
 * Adapted from the spec's literal `fetchEndpoint` prop to a controlled component: the calling
 * screen owns the React Query hook and passes `data`/`isLoading` down, keeping this component
 * free of data-fetching concerns.
 */
export function DataGrid<T extends { id: number }>({
  columns,
  data,
  isLoading,
  search,
  onSearchChange,
  page,
  onPageChange,
  onRowClick,
  exportFileName
}: DataGridProps<T>) {
  const { t } = useTranslation();
  const rights = useScreenRights();
  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1;

  const exportColumns: ExportColumn<T>[] = columns
    .filter((c) => c.exportValue)
    .map((c) => ({ header: c.label, value: c.exportValue! }));

  return (
    <div style={{ background: 'var(--color-surface)', borderTop: '3px solid var(--color-gold-500)', borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-2)', overflow: 'hidden' }}>
      <div style={{ padding: 12, borderBottom: '1px solid var(--color-border)', display: 'flex', justifyContent: 'space-between', gap: 8 }}>
        <Input placeholder={t('common.search')} value={search} onChange={(e) => onSearchChange(e.target.value)} style={{ maxWidth: 280 }} />
        {exportFileName && data && exportColumns.length > 0 && (rights.export || rights.print) && (
          <div style={{ display: 'flex', gap: 8 }}>
            {rights.print && (
              <Button variant="secondary" onClick={() => printTable(data.items, exportColumns, exportFileName)}>
                <Icon name="printer" size={14} />
                {t('common.print')}
              </Button>
            )}
            {rights.export && <ExportMenu rows={data.items} columns={exportColumns} fileName={exportFileName} title={exportFileName} />}
          </div>
        )}
      </div>

      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--color-navy-700)' }}>
              {columns.map((c) => (
                <th key={c.key} style={{ textAlign: 'start', padding: '11px 14px', color: 'rgba(255,255,255,0.9)', fontWeight: 700, fontSize: 12, whiteSpace: 'nowrap' }}>
                  {c.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <SkeletonRows rows={5} columns={columns.length} />
            ) : data && data.items.length > 0 ? (
              data.items.map((row, i) => (
                <tr
                  key={row.id}
                  onClick={() => onRowClick?.(row)}
                  style={{
                    cursor: onRowClick ? 'pointer' : 'default',
                    borderBottom: '1px solid var(--color-border)',
                    background: i % 2 === 1 ? 'var(--color-surface-2)' : 'transparent'
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--color-info-bg)')}
                  onMouseLeave={(e) => (e.currentTarget.style.background = i % 2 === 1 ? 'var(--color-surface-2)' : 'transparent')}
                >
                  {columns.map((c) => (
                    <td key={c.key} style={{ padding: '10px 14px', fontSize: 13 }}>
                      {c.render(row)}
                    </td>
                  ))}
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={columns.length} style={{ padding: 24, textAlign: 'center', color: 'var(--color-text-muted)' }}>
                  {t('common.noData')}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: 12 }}>
        <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>
          {data ? `${t('common.total')}: ${data.totalCount}` : ''}
        </span>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button variant="ghost" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>{t('common.previous')}</Button>
          <span style={{ fontSize: 12, alignSelf: 'center' }}>{page} / {totalPages}</span>
          <Button variant="ghost" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>{t('common.next')}</Button>
        </div>
      </div>
    </div>
  );
}
