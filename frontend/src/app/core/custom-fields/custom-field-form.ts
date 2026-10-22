import { FormControl, FormGroup, ValidatorFn, Validators } from '@angular/forms';
import { CustomFieldValueMap } from '../api/api.models';
import { CustomFieldDataType, FormFieldMeta, FormSectionMeta } from './custom-field.models';

/** Builds the nested `customFields` FormGroup from metadata (one control per active field). */
export function buildCustomFieldsGroup(
  sections: readonly FormSectionMeta[],
  values: CustomFieldValueMap | null | undefined,
  isCreate: boolean,
): FormGroup<Record<string, FormControl<unknown>>> {
  const controls: Record<string, FormControl<unknown>> = {};
  for (const field of sections.flatMap((s) => s.fields)) {
    const raw = values?.[field.key] ?? (isCreate ? field.defaultValue : null) ?? emptyValue(field.dataType);
    controls[field.key] = new FormControl<unknown>(fromApiValue(field.dataType, raw), validatorsFor(field));
  }
  return new FormGroup(controls);
}

export function validatorsFor(f: FormFieldMeta): ValidatorFn[] {
  const v: ValidatorFn[] = [];
  if (f.required && f.dataType !== 'bool') v.push(Validators.required);
  if (f.dataType === 'text') {
    if (f.minLength != null) v.push(Validators.minLength(f.minLength));
    v.push(Validators.maxLength(f.maxLength ?? 4000));
    if (f.pattern) v.push(Validators.pattern(f.pattern));
  }
  if (f.dataType === 'number' || f.dataType === 'decimal') {
    if (f.min != null) v.push(Validators.min(f.min));
    if (f.max != null) v.push(Validators.max(f.max));
  }
  return v;
}

function emptyValue(t: CustomFieldDataType): unknown {
  return t === 'multiSelect' ? [] : t === 'bool' ? false : null;
}

/** API canonical value → control value (nz-date-picker wants Date). */
export function fromApiValue(t: CustomFieldDataType, v: unknown): unknown {
  if (v == null || v === '') return emptyValue(t);
  switch (t) {
    case 'date': {
      const [y, m, d] = String(v).slice(0, 10).split('-').map(Number);
      return new Date(y, m - 1, d); // local midnight — avoids TZ day shifts
    }
    case 'dateTime':
      return new Date(String(v));
    case 'multiSelect':
      return Array.isArray(v) ? v : [v];
    default:
      return v;
  }
}

/** Control values → canonical API payload; empty values are omitted (server applies "required"). */
export function toApiValues(
  sections: readonly FormSectionMeta[],
  raw: Record<string, unknown>,
): CustomFieldValueMap {
  const out: CustomFieldValueMap = {};
  for (const f of sections.flatMap((s) => s.fields)) {
    const v = raw[f.key];
    if (v == null || v === '' || (Array.isArray(v) && v.length === 0)) continue;
    switch (f.dataType) {
      case 'date':
        out[f.key] = toIsoDate(v as Date);
        break;
      case 'dateTime':
        out[f.key] = (v as Date).toISOString();
        break;
      case 'number':
      case 'decimal':
        out[f.key] = Number(v);
        break;
      default:
        out[f.key] = v;
    }
  }
  return out;
}

export function toIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
