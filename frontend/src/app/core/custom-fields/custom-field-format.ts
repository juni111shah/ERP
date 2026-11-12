import { FormFieldMeta } from './custom-field.models';

/** Formats a custom-field value for list cells: yes/no for switches, choice labels for selects, the plain value otherwise. */
export function formatCustomValue(field: FormFieldMeta, value: unknown): string {
  if (value == null || value === '') return '';
  if (field.dataType === 'bool') return value ? 'Yes' : 'No';
  const label = (v: unknown) => field.options.find((o) => o.value === v)?.label ?? String(v);
  if (field.dataType === 'multiSelect' && Array.isArray(value)) return value.map(label).join(', ');
  if (field.dataType === 'select') return label(value);
  return String(value);
}
