import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { FilterCriterion, ListQuery, PagedResult } from '../../core/api/api.models';
import { formatMoney } from './invoice-sheet.component';

/** The API serves at most 200 rows a page. The cards read at most this many rows, and say so when there are more. */
const KPI_PAGE_SIZE = 200;
export const KPI_MAX_ROWS = 5000;

type Source<T> = (q: ListQuery) => Observable<PagedResult<T>>;
export interface AllRows<T> { items: T[]; total: number; capped: boolean; }

/** The list endpoints have no totals, so a card that needs a true sum reads every matching row, not just the page on screen. */
export function fetchAllRows<T>(source: Source<T>, filters: FilterCriterion[], sort?: string): Observable<AllRows<T>> {
  const page = (n: number) => source({ page: n, pageSize: KPI_PAGE_SIZE, sort, filters });
  return page(1).pipe(
    expand((r) => (r.hasNext && r.page * KPI_PAGE_SIZE < KPI_MAX_ROWS ? page(r.page + 1) : EMPTY)),
    reduce((all, r) => ({ items: all.items.concat(r.items), total: r.total }), { items: [] as T[], total: 0 }),
    map((all) => ({ ...all, capped: all.items.length < all.total })),
  );
}

/** How many rows match a filter. The server counts them, so nothing is loaded. */
export function countRows<T>(source: Source<T>, filters: FilterCriterion[]): Observable<number> {
  return source({ page: 1, pageSize: 1, filters }).pipe(map((r) => r.total));
}

/** A sum per currency, for example "QAR 1,200.00 · USD 50.00". With no rows it is zero in the given currency. */
export function moneyByCurrency<T extends { currencyCode: string }>(rows: T[], pick: (r: T) => number, emptyCurrency: string): string {
  const by = new Map<string, number>();
  for (const r of rows) by.set(r.currencyCode, (by.get(r.currencyCode) ?? 0) + pick(r));
  return by.size ? [...by].map(([c, v]) => formatMoney(v, c)).join(' · ') : formatMoney(0, emptyCurrency);
}

/** "1 invoice", "3 invoices". */
export function countOf(n: number, one: string, many = `${one}s`): string {
  return `${n.toLocaleString('en-US')} ${n === 1 ? one : many}`;
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
export const startOfMonth = (today = new Date()) => iso(new Date(today.getFullYear(), today.getMonth(), 1));
export const startOfYear = (today = new Date()) => iso(new Date(today.getFullYear(), 0, 1));
