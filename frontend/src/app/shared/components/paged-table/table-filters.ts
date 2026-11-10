/** The header filters of <erp-paged-table>: when one is set, the criteria sent to the server and the test of a loaded row. */
import { FilterCriterion } from '@core/api/api.models';
import { toIsoDate } from '@core/format/dates';
import { ColumnFilterKind, FilterOption, TableColumn, humanizeValue, rawCell, readCell } from './table-column';

/** One column's filter, as edited in its dropdown. */
export interface FilterState { text?: string; values?: string[]; range?: (Date | null)[] | null; min?: number | null; max?: number | null; }

/** The calendar day of a picked date (local), or null. */
const day = (d: Date | null | undefined): string | null => (d ? toIsoDate(d) : null);

/** Whether a filter holds anything. */
export function isFilterSet(f: FilterState | undefined): boolean {
  if (!f) return false;
  return !!f.text?.trim() || !!f.values?.length || !!(f.range && (f.range[0] || f.range[1])) || f.min != null || f.max != null;
}

/** The server criteria of the filters set on columns that have a `filterField`. */
export function serverCriteria<T>(columns: TableColumn<T>[], kinds: Record<string, ColumnFilterKind | null>, f: Record<string, FilterState>): FilterCriterion[] {
  const out: FilterCriterion[] = [];
  for (const c of columns) {
    const s = f[c.key];
    const field = c.filterField;
    if (!field || !isFilterSet(s)) continue;
    switch (kinds[c.key]) {
      case 'text': out.push({ field, op: 'contains', value: s.text!.trim() }); break;
      case 'select': out.push(s.values!.length === 1 ? { field, op: 'eq', value: s.values![0] } : { field, op: 'in', value: s.values! }); break;
      case 'date': {
        const from = day(s.range?.[0]); const to = s.range?.[1];
        if (from) out.push({ field, op: 'gte', value: from });
        // Before the next day, so the end date counts in full for fields that also hold a time.
        if (to) out.push({ field, op: 'lt', value: day(new Date(to.getFullYear(), to.getMonth(), to.getDate() + 1))! });
        break;
      }
      case 'number':
        if (s.min != null) out.push({ field, op: 'gte', value: s.min });
        if (s.max != null) out.push({ field, op: 'lte', value: s.max });
        break;
    }
  }
  return out;
}

/** Whether a loaded row passes a column's filter (for columns filtered in the browser). */
export function rowMatches<T>(row: T, c: TableColumn<T>, f: FilterState, kind: ColumnFilterKind): boolean {
  const raw = rawCell(row, c);
  switch (kind) {
    case 'text': {
      const q = f.text!.trim().toLowerCase();
      return String(readCell(row, c)).toLowerCase().includes(q) || (raw != null && typeof raw !== 'object' && String(raw).toLowerCase().includes(q));
    }
    case 'select': {
      const vals = (Array.isArray(raw) ? raw : [raw]).filter((v) => v !== null && v !== undefined).map((v) => String(v));
      return !f.values?.length || vals.some((v) => f.values!.includes(v));
    }
    case 'date': {
      const d = raw == null ? '' : raw instanceof Date ? day(raw) ?? '' : String(raw).slice(0, 10);
      const from = day(f.range?.[0]); const to = day(f.range?.[1]);
      if (!d) return !from && !to;
      return (!from || d >= from) && (!to || d <= to);
    }
    case 'number': {
      const n = typeof raw === 'number' ? raw : parseFloat(String(readCell(row, c)).replace(/[^0-9.\-]/g, ''));
      if (Number.isNaN(n)) return f.min == null && f.max == null;
      return (f.min == null || n >= f.min) && (f.max == null || n <= f.max);
    }
  }
}

/**
 * The select-filter options seen so far plus the values of the rows just loaded (for select columns without configured options),
 * sorted by label; null when nothing new turned up. Remembering them keeps the options once the server filters the rows.
 */
export function collectSeenOptions<T>(seen: Record<string, FilterOption[]>, items: T[], columns: TableColumn<T>[], kinds: Record<string, ColumnFilterKind | null>): Record<string, FilterOption[]> | null {
  const next = { ...seen };
  let changed = false;
  for (const c of columns) {
    if (kinds[c.key] !== 'select' || c.filterOptions) continue;
    const known = new Map((next[c.key] ?? []).map((o) => [o.value, o]));
    const before = known.size;
    for (const r of items) {
      const raw = rawCell(r, c);
      const vals = Array.isArray(raw) ? raw : [raw];
      for (const v of vals) {
        if (v === null || v === undefined || v === '') continue;
        const value = String(v);
        if (known.has(value)) continue;
        const shown = Array.isArray(raw) ? value : readCell(r, c);
        const label = typeof v === 'boolean' ? (v ? 'Yes' : 'No') : shown !== '' && String(shown) !== value ? String(shown) : humanizeValue(value);
        known.set(value, { value, label });
      }
    }
    if (known.size !== before) { next[c.key] = [...known.values()].sort((a, b) => a.label.localeCompare(b.label)); changed = true; }
  }
  return changed ? next : null;
}
