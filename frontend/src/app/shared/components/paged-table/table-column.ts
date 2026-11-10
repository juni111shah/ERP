/** How the cells of a column of <erp-paged-table> are read, shown, aligned and filtered. */
import { displayDate, displayLabel, isIsoDate } from '@core/format/display';

export type ColumnFilterKind = 'text' | 'select' | 'date' | 'number';
export type ColumnPin = 'left' | 'right';

export interface TableColumn<T> {
  key: string;
  label: string;
  /** server sort field ([Sortable] on the entity); omit to disable sorting */
  sortField?: string;
  width?: string;
  /** 'right' for amounts. When omitted, a column holding numbers or formatted amounts is right-aligned and any other column follows the text direction. */
  align?: 'left' | 'right';
  /** cell text; defaults to row[key] */
  value?: (row: T) => unknown;
  /** Header filter kind. Inferred from the key and the loaded values when omitted; `false` turns the filter off. */
  filter?: ColumnFilterKind | false;
  /**
   * Server filter field ([Filterable] on the entity). When set the filter is sent as `filter=field:op:value` (select → eq/in,
   * date → gte/lte, number → gte/lte, text → contains); otherwise the column filters the rows already loaded on the current page.
   */
  filterField?: string;
  /** Options for a select filter (raw values, optionally with labels). Defaults to the values seen in the loaded rows. */
  filterOptions?: readonly (string | { value: string; label: string })[];
  /** Raw value used for filtering; defaults to row[key], else value(row). */
  filterValue?: (row: T) => unknown;
  /** Pinned by default (users can change it; the first column is pinned to the start by default when the table scrolls sideways). */
  sticky?: ColumnPin;
  /** Hidden until the user turns it on in the Columns panel. */
  hidden?: boolean;
}

/** A choice of a select filter. */
export interface FilterOption { value: string; label: string; }

/** What a blank cell shows, so a missing value is not mistaken for zero or for a row still loading. */
export const EMPTY_CELL = '—';

/** Keys whose values are API words (statuses, kinds...): shown in words and filtered with a select. */
const ENUM_KEY = /^(status|stage|kind|type|usage|priority|category|source|sourceType|direction|method|action|roles?|isActive|isCreditNote|currency|currencyCode|dealType|channel|entityType|recordType)$/i;
const STATUS_KEY = /^(status|stage|state|paymentStatus|approvalStatus)$/;
const DATE_KEY = /(date|Date|On|At|Ends)$/;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}/;
/** Identifiers made of digits read as text, so they stay at the start of the cell. */
const IDENTIFIER_KEY = /(code|number|no|phone|mobile|year|id|ref|reference)$/i;
/** A formatted amount: optional currency code, grouped digits, optional decimals or percent. Plain digits alone could be a code. */
const AMOUNT_TEXT = /^(-?[A-Z]{3}\s)?-?\d[\d,]*(\.\d+|,\d{3})\s?%?$|^-?\d+(\.\d+)?\s?%$/;

/** "closedWon" or "on_hold" in words: "Closed won", "On hold". */
export function humanizeValue(v: string): string {
  const s = v.replace(/[._-]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim().toLowerCase();
  return s ? s[0].toUpperCase() + s.slice(1) : v;
}

/** The cell text: value(row) or row[key], lists joined, booleans as Yes/No, nothing as ''. Sorting, filtering and exports use it. */
export function readCell<T>(row: T, c: TableColumn<T>): unknown {
  const v = c.value ? c.value(row) : (row as Record<string, unknown>)[c.key];
  return Array.isArray(v) ? v.join(', ') : typeof v === 'boolean' ? (v ? 'Yes' : 'No') : (v ?? '');
}

/** The raw value a column filters on: filterValue(row), else row[key], else value(row). */
export function rawCell<T>(row: T, c: TableColumn<T>): unknown {
  if (c.filterValue) return c.filterValue(row);
  const v = (row as Record<string, unknown>)[c.key];
  return v !== undefined ? v : c.value ? c.value(row) : undefined;
}

/** What a cell shows: dates in the shared format and API values in words; a blank cell shows {@link EMPTY_CELL}. */
export function displayCell<T>(row: T, c: TableColumn<T>): unknown {
  const v = readCell(row, c);
  if (v === '' || v === null || v === undefined) return c.key === 'actions' ? '' : EMPTY_CELL;
  if (isIsoDate(v)) return displayDate(v);
  return ENUM_KEY.test(c.key) && !c.value ? displayLabel(v) : v;
}

/** A status column (by its key, without a custom value) is drawn as a status tag. */
export function isStatusColumn<T>(c: TableColumn<T>): boolean {
  return !c.value && STATUS_KEY.test(c.key);
}

/** Numbers and formatted amounts line up at the end of the cell, judged by the first filled cell; identifiers and text follow the text. */
export function inferredAlign<T>(c: TableColumn<T>, items: T[]): 'right' | null {
  if (c.key === 'actions') return 'right';
  if (IDENTIFIER_KEY.test(c.key)) return null;
  for (const row of items) {
    const shown = readCell(row, c);
    if (shown === '' || shown === null || shown === undefined) continue;
    return typeof shown === 'number' || (typeof shown === 'string' && AMOUNT_TEXT.test(shown.trim())) ? 'right' : null;
  }
  return null;
}

/** The header filter of a column: as configured, else guessed from the key and the first filled value; null for none. */
export function filterKindOf<T>(c: TableColumn<T>, items: T[]): ColumnFilterKind | null {
  if (c.filter === false || c.key === 'actions') return null;
  if (c.filter) return c.filter;
  if (c.filterOptions) return 'select';
  if (ENUM_KEY.test(c.key)) return 'select';
  if (DATE_KEY.test(c.key)) return 'date';
  const sample = items.map((r) => rawCell(r, c)).find((v) => v !== null && v !== undefined && v !== '');
  if (typeof sample === 'boolean') return 'select';
  if (typeof sample === 'number') return 'number';
  if (typeof sample === 'string' && ISO_DATE.test(sample)) return 'date';
  if (c.align === 'right') return 'number';
  return 'text';
}

/** The configured options of a select filter, with raw values put in words. */
export function configuredOptions<T>(c: TableColumn<T>): FilterOption[] | undefined {
  return c.filterOptions?.map((o) => (typeof o === 'string' ? { value: o, label: humanizeValue(o) } : o));
}
