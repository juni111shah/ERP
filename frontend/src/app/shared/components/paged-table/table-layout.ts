/** The user's column layout of <erp-paged-table> (order, shown/hidden, pins) and where the browser keeps it. */
import { ColumnPin, TableColumn } from './table-column';

/** What the user changed: column order, columns hidden or shown against their default, and pins ('none' = unpinned on purpose). */
export interface TableLayout { order: string[]; hidden: string[]; shown: string[]; pins: Record<string, ColumnPin | 'none'>; }
/** One column as the table draws it. */
export interface ColumnView<T> { col: TableColumn<T>; key: string; pin: ColumnPin | null; hidden: boolean; }

export const EMPTY_LAYOUT: TableLayout = { order: [], hidden: [], shown: [], pins: {} };

/** A column's pin when the user chose none: its own, the actions column at the end, the first visible column at the start. */
function defaultPin<T>(c: TableColumn<T>, first: boolean): ColumnPin | null {
  if (c.sticky) return c.sticky;
  if (c.key === 'actions') return 'right';
  return first ? 'left' : null;
}

/** Every column in the user's order (a column the layout does not know goes after its left neighbour), with its pin and visibility. */
export function layoutColumns<T>(cols: TableColumn<T>[], l: TableLayout): ColumnView<T>[] {
  const keys = cols.map((c) => c.key);
  const order = l.order.filter((k) => keys.includes(k));
  keys.forEach((k, i) => {
    if (order.includes(k)) return;
    const prev = keys.slice(0, i).reverse().find((p) => order.includes(p));
    order.splice(prev ? order.indexOf(prev) + 1 : 0, 0, k);
  });
  const byKey = new Map(cols.map((c) => [c.key, c]));
  const isHidden = (k: string) => l.hidden.includes(k) || (!!byKey.get(k)!.hidden && !l.shown.includes(k));
  const first = order.find((k) => !isHidden(k));
  return order.map((k) => {
    const col = byKey.get(k)!;
    const saved = l.pins[k];
    const pin: ColumnPin | null = saved === 'none' ? null : saved ?? defaultPin(col, k === first);
    return { col, key: k, pin, hidden: isHidden(k) };
  });
}

/** The columns drawn: start-pinned first, then free, then end-pinned (sticky cells must sit at the edges); never none at all. */
export function renderedColumns<T>(all: ColumnView<T>[]): ColumnView<T>[] {
  let v = all.filter((c) => !c.hidden);
  if (!v.length) v = all.slice(0, 1);
  return [...v.filter((c) => c.pin === 'left'), ...v.filter((c) => !c.pin), ...v.filter((c) => c.pin === 'right')];
}

/** The layout with one column turned on or off. */
export function withVisibility(l: TableLayout, key: string, visible: boolean): TableLayout {
  return {
    ...l,
    hidden: visible ? l.hidden.filter((k) => k !== key) : [...l.hidden.filter((k) => k !== key), key],
    shown: visible ? [...l.shown.filter((k) => k !== key), key] : l.shown.filter((k) => k !== key),
  };
}

/** The saved layout under `key`, cleaned of anything malformed; the empty layout when none or storage is unavailable. */
export function readLayout(key: string): TableLayout {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return EMPTY_LAYOUT;
    const p = JSON.parse(raw) as Partial<TableLayout>;
    const arr = (a: unknown) => (Array.isArray(a) ? a.filter((x): x is string => typeof x === 'string') : []);
    return { order: arr(p.order), hidden: arr(p.hidden), shown: arr(p.shown), pins: p.pins && typeof p.pins === 'object' ? p.pins : {} };
  } catch { return EMPTY_LAYOUT; }
}

/** Saves a layout, or forgets it (null). */
export function writeLayout(key: string, layout: TableLayout | null): void {
  try {
    if (layout) localStorage.setItem(key, JSON.stringify(layout));
    else localStorage.removeItem(key);
  } catch { /* storage unavailable */ }
}
