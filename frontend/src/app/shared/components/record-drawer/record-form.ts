/** The field specs of <erp-record-drawer> and the pure rules behind them: building the form, option groups, and the saved values. */
import { FormControl, FormGroup, ValidatorFn, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';

export type FieldType = 'text' | 'password' | 'textarea' | 'number' | 'date' | 'select' | 'multiselect' | 'switch' | 'lookup' | 'rows';
/** One column of a `rows` field (a small table the user adds lines to). */
export interface RowColumn { key: string; label: string; type: 'text' | 'number' | 'date' | 'select'; options?: SelectOption[]; width?: string; }
/** `group` puts options under a heading in a `select`. */
export interface SelectOption { value: string | number | boolean; label: string; group?: string; }

/** Declarative description of one form field. The drawer renders, validates and serialises it. */
export interface FieldSpec {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  options?: SelectOption[];
  /** a `select` long enough to want a search box */
  searchable?: boolean;
  /** server-side search for `lookup` fields (returns id + label) */
  lookup?: (term: string) => Observable<LookupOption[]>;
  /** label of the currently selected lookup value when editing (the search has not run yet) */
  currentLabel?: (record: Record<string, unknown>) => string | undefined;
  /** columns of a `rows` field; its value is a list of objects with these keys */
  columns?: RowColumn[];
  /** the cells a new line of a `rows` field starts with */
  rowDefaults?: Record<string, unknown>;
  /** text shown under a `rows` field from its current lines, such as a total */
  summary?: (rows: Record<string, unknown>[]) => string;
  min?: number;
  max?: number;
  maxLength?: number;
  precision?: number;
  hint?: string;
  /** Values offered while typing in a `text` field; anything else can still be typed. */
  suggestions?: string[];
  default?: unknown;
  /** cannot be changed once the record exists */
  readonlyOnEdit?: boolean;
  /** hide the field depending on the other values */
  hidden?: (value: Record<string, unknown>) => boolean;
  /**
   * Suggest other values when the user changes this field: return the keys to set (e.g. switch "registered" on once a number is
   * typed), or null for no change. Only keys whose value differs are written, and nothing runs while the form is being built.
   * `previous` is the form as it was before this change (e.g. to tell an auto-filled value from one the user picked).
   */
  onChange?: (value: unknown, form: Record<string, unknown>, previous: Record<string, unknown>) => Record<string, unknown> | null;
}

/** The form of the fields, filled from the record being edited (null: a new record, with the field defaults). */
export function createRecordForm(fields: FieldSpec[], record: Record<string, unknown> | null): FormGroup {
  const controls: Record<string, FormControl> = {};
  for (const f of fields) {
    const initial = record ? (record[f.key] ?? null) : (f.default ?? (f.type === 'switch' ? false : null));
    const validators: ValidatorFn[] = [];
    if (f.required && f.type !== 'switch') validators.push(Validators.required);
    if (f.type === 'text' || f.type === 'textarea') validators.push(Validators.maxLength(f.maxLength ?? 2000));
    controls[f.key] = new FormControl({ value: f.type === 'date' ? toFormDate(initial) : initial, disabled: !!record && !!f.readonlyOnEdit }, validators);
  }
  return new FormGroup(controls);
}

/** A stored day ("2026-11-10") as a date picker value at local midnight, so the day does not shift with the time zone. */
export function toFormDate(v: unknown): Date | null {
  if (!v) return null;
  if (v instanceof Date) return v; // a field default may be given as a Date (for example "today")
  const [y, m, d] = String(v).slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d); // local midnight: no timezone day shift
}

/** The options under their headings, in the order they were given; options without a heading come under "Other". */
export function groupOptions(f: FieldSpec): { label: string; options: SelectOption[] }[] {
  const groups = new Map<string, SelectOption[]>();
  for (const o of f.options ?? []) groups.set(o.group ?? 'Other', [...(groups.get(o.group ?? 'Other') ?? []), o]);
  return [...groups].map(([label, options]) => ({ label, options }));
}

/** The options of a select cell, plus the value the line already holds when it is not among them. */
export function cellOptions(col: RowColumn, current: unknown): SelectOption[] {
  const options = col.options ?? [];
  return typeof current === 'string' && current && !options.some((o) => o.value === current) ? [...options, { value: current, label: current }] : options;
}

/** A field's value as the API takes it: blanks as null, dates as days, text trimmed, `rows` cleaned. */
export function serialiseField(f: FieldSpec, v: unknown): unknown {
  if (f.type === 'rows') return cleanRows(f, v);
  if (v === '' || v === undefined) return null;
  if (f.type === 'date') return v instanceof Date ? toIsoDate(v) : v;
  if (f.type === 'text' || f.type === 'textarea') return typeof v === 'string' ? v.trim() || null : v;
  return v;
}

/** Lines with every cell empty are dropped; number cells that are not numbers are left out. */
function cleanRows(f: FieldSpec, v: unknown): Record<string, unknown>[] {
  const cols = f.columns ?? [];
  return (Array.isArray(v) ? v : []).map((r: Record<string, unknown>) => {
    const out: Record<string, unknown> = {};
    for (const c of cols) {
      const cell = r[c.key];
      if (c.type === 'number') { if (typeof cell === 'number' && Number.isFinite(cell)) out[c.key] = cell; }
      else if (typeof cell === 'string' && cell.trim()) out[c.key] = cell.trim();
    }
    return out;
  }).filter((r) => Object.keys(r).length > 0);
}
