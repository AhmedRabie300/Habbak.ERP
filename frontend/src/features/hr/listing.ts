import type { PagedResult } from '../../app/apiTypes';

/**
 * The HR lookup lists are short (a company's job grades, positions...), so they come back whole and
 * are searched and paged in the browser — the same shape the DataGrid expects from a paged endpoint
 * (same convention as features/fixedAssets/listing.ts).
 */
export function toPaged<T>(rows: T[] | undefined, search: string, matches: (row: T, query: string) => boolean): PagedResult<T> {
  const query = search.trim().toLowerCase();
  const items = (rows ?? []).filter((row) => (query ? matches(row, query) : true));
  return { items, totalCount: items.length, page: 1, pageSize: Math.max(items.length, 1) };
}
