import { BusinessProfile, NumberSequenceView } from '@core/api/setup.api';

/** The text a number series produces: `{prefix}`, `{yyyy}`, `{yy}` and `{seq:N}` (the next number, padded to N digits) filled in. */
export function sequenceExample(pattern: string, prefix: string, next: number, today = new Date()): string {
  const year = String(today.getFullYear());
  return pattern
    .replace(/\{prefix\}/g, prefix).replace(/\{yyyy\}/g, year).replace(/\{yy\}/g, year.slice(2))
    .replace(/\{seq:(\d+)\}/g, (_, digits: string) => String(next).padStart(Number(digits), '0'));
}

/** One numbering row as the page edits it: the edited values, what is saved, and the row's save state. */
export interface SequenceRow extends NumberSequenceView { saved: NumberSequenceView; saving: boolean; error: string | null; }

/** A saved numbering as a clean row. */
export function sequenceRow(s: NumberSequenceView): SequenceRow {
  return { ...s, saved: s, saving: false, error: null };
}

/** The numbering rows with the one for `documentType` changed. */
export function updateSequenceRow(rows: SequenceRow[], documentType: string, change: (row: SequenceRow) => SequenceRow): SequenceRow[] {
  return rows.map((r) => (r.documentType === documentType ? change(r) : r));
}

/** The row's prefix, pattern or yearly reset differ from what is saved. */
export function isSequenceDirty(s: SequenceRow): boolean {
  return s.prefix !== s.saved.prefix || s.pattern !== s.saved.pattern || s.resetYearly !== s.saved.resetYearly;
}

/** The capabilities switched off now differ from the profile's. */
export function capabilitiesChanged(profile: BusinessProfile | null, disabled: string[]): boolean {
  const was = new Set(profile?.capabilities.filter((c) => !c.enabled).map((c) => c.key));
  const now = new Set(disabled);
  return was.size !== now.size || [...now].some((k) => !was.has(k));
}

/** The units or item categories differ from the profile's (order counts). */
export function listsChanged(profile: BusinessProfile | null, units: string[], categories: string[]): boolean {
  return !!profile && (JSON.stringify(units) !== JSON.stringify(profile.units) || JSON.stringify(categories) !== JSON.stringify(profile.itemCategories));
}
