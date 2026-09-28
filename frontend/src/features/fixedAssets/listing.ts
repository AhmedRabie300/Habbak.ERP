import { useMemo } from 'react';
import type { PagedResult } from '../../app/apiTypes';
import { useAccountTree } from '../accounting/chartOfAccounts/api';

/**
 * The module's lists are short (a company's assets, its open jobs), so they come back whole and are
 * searched and paged in the browser — the same shape the DataGrid expects from a paged endpoint.
 */
export function toPaged<T>(rows: T[] | undefined, search: string, matches: (row: T, query: string) => boolean): PagedResult<T> {
  const query = search.trim().toLowerCase();
  const items = (rows ?? []).filter((row) => (query ? matches(row, query) : true));
  return { items, totalCount: items.length, page: 1, pageSize: Math.max(items.length, 1) };
}

/** Postable accounts, for every account picker in the module. */
export function usePostableAccountOptions() {
  const { data: accounts } = useAccountTree();
  return useMemo(
    () =>
      (accounts ?? [])
        .filter((a) => a.isPostable && a.isActive)
        .map((a) => ({ value: a.id, label: `${a.code} — ${a.nameAr}` })),
    [accounts]
  );
}

export const today = () => new Date().toISOString().slice(0, 10);

export function money(value: number | null | undefined) {
  return (value ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
