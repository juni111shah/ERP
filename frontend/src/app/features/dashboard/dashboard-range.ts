import { ParamMap } from '@angular/router';
import { toIsoDate } from '@core/format/dates';

/** The overview's period rules: presets, custom ranges (at most 5 years, ending today or earlier), the URL and the browser's last choice. */

/** Overview period presets, shortest first, offered in the range dropdown next to `custom` (a picked date range). */
export type RangePreset = '1m' | '3m' | '6m' | '1y' | '2y' | '3y' | 'custom';
export type FixedPreset = Exclude<RangePreset, 'custom'>;
export interface DashboardRange { preset: RangePreset; from: string; to: string; }

export const RANGE_PRESETS: { key: FixedPreset; months: number; labelKey: string; label: string }[] = [
  { key: '1m', months: 1, labelKey: 'ui.overview.range.preset.1m', label: 'Last month' },
  { key: '3m', months: 3, labelKey: 'ui.overview.range.preset.3m', label: 'Last 3 months' },
  { key: '6m', months: 6, labelKey: 'ui.overview.range.preset.6m', label: 'Last 6 months' },
  // "Last year" is the trailing 12 months ending today, not the previous calendar year
  { key: '1y', months: 12, labelKey: 'ui.overview.range.preset.1y', label: 'Last year' },
  { key: '2y', months: 24, labelKey: 'ui.overview.range.preset.2y', label: 'Last 2 years' },
  { key: '3y', months: 36, labelKey: 'ui.overview.range.preset.3y', label: 'Last 3 years' },
];
/** Longest period the overview accepts (the API enforces the same 5-year limit). */
export const MAX_RANGE_MONTHS = 60;
export const RANGE_KEY = 'erp.overview.range';
export const DEFAULT_PRESET = '1y' as const;

/** The local calendar day as yyyy-MM-dd. */
export const isoDate = toIsoDate;
export const parseIso = (s: string) => { const [y, m, d] = s.split('-').map(Number); return new Date(y, m - 1, d); };
export const startOfToday = () => { const n = new Date(); return new Date(n.getFullYear(), n.getMonth(), n.getDate()); };
export const addDays = (d: Date, n: number) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);

/** Same day n months earlier, clamped to the month's last day (31 Mar minus 1 month = 28/29 Feb), like the server's AddMonths. */
export function minusMonths(d: Date, n: number): Date {
  const t = new Date(d.getFullYear(), d.getMonth() - n, 1);
  t.setDate(Math.min(d.getDate(), new Date(t.getFullYear(), t.getMonth() + 1, 0).getDate()));
  return t;
}

/** First day of an n-month period ending on `end` inclusive: one day after the same day n months back (1Y = 31 Oct 2025 – 30 Oct 2026). */
export const periodStart = (end: Date, months: number) => addDays(minusMonths(end, months), 1);

export function presetRange(preset: FixedPreset, end = startOfToday()): DashboardRange {
  const months = RANGE_PRESETS.find((p) => p.key === preset)!.months;
  return { preset, from: isoDate(periodStart(end, months)), to: isoDate(end) };
}

const ISO = /^\d{4}-\d{2}-\d{2}$/;
const isPreset = (key: unknown): key is FixedPreset => RANGE_PRESETS.some((p) => p.key === key);

/** A real calendar date in yyyy-MM-dd (rejects 2026-02-31). */
export const isRealDate = (s: unknown): s is string => typeof s === 'string' && ISO.test(s) && isoDate(parseIso(s)) === s;

/** A usable period: real dates, from ≤ to, to not after today, and at most 5 years long. */
export function validRange(from: unknown, to: unknown, today = startOfToday()): boolean {
  if (!isRealDate(from) || !isRealDate(to) || from > to || to > isoDate(today)) return false;
  return from >= isoDate(periodStart(parseIso(to), MAX_RANGE_MONTHS));
}

/** A picker day that cannot be chosen: a future day, or (once `anchor` is picked) one that would make the period longer than 5 years. */
export function blockedDay(d: Date, anchor: Date | null, today = startOfToday()): boolean {
  const day = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  if (day > today) return true;
  if (!anchor) return false;
  const [f, t] = day < anchor ? [day, anchor] : [anchor, day];
  return !validRange(isoDate(f), isoDate(t), today);
}

/** A period from query params: `?range=1y`, or `?from=2025-10-31&to=2026-10-30` for a custom period. Invalid params give null. */
export function rangeFromParams(p: ParamMap, today = startOfToday()): DashboardRange | null {
  const preset = p.get('range');
  if (preset && isPreset(preset)) return presetRange(preset, today);
  const from = p.get('from'), to = p.get('to');
  if (validRange(from, to, today)) return { preset: 'custom', from: from!, to: to! };
  return null;
}

export const hasRangeParams = (p: ParamMap) => p.has('range') || p.has('from') || p.has('to');

/** The last choice in this browser; relative presets are recomputed against today, custom periods are re-validated. */
export function storedRange(today = startOfToday()): DashboardRange | null {
  try {
    const saved = JSON.parse(localStorage.getItem(RANGE_KEY) ?? 'null') as DashboardRange | null;
    if (saved?.preset === 'custom' && validRange(saved.from, saved.to, today)) return { preset: 'custom', from: saved.from, to: saved.to };
    if (saved && isPreset(saved.preset)) return presetRange(saved.preset, today);
  } catch { /* storage unavailable or corrupt */ }
  return null;
}

/** Remembers the choice in this browser for the next visit. */
export function rememberRange(r: DashboardRange): void {
  try { localStorage.setItem(RANGE_KEY, JSON.stringify(r)); } catch { /* storage unavailable */ }
}

/** The period a page opens with: the URL, else this browser's last choice, else the "Last year" default. */
export function initialRange(p: ParamMap): DashboardRange {
  return rangeFromParams(p) ?? storedRange() ?? presetRange(DEFAULT_PRESET);
}

export const sameRange = (a: DashboardRange, b: DashboardRange) => a.preset === b.preset && a.from === b.from && a.to === b.to;

/** The query params that name a period (the other kind's params are cleared). */
export const rangeParams = (r: DashboardRange) => (r.preset === 'custom' ? { range: null, from: r.from, to: r.to } : { range: r.preset, from: null, to: null });
