import { displayDate } from '@core/format/display';

const blank = (v: unknown): boolean => v === null || v === undefined || v === '';

/** A declared field's value in a list cell: dates as the person reads them, yes/no for a switch, empty when not set. */
export function listCellText(type: string, v: unknown): string {
  if (blank(v)) return '';
  if (type === 'bool') return v ? 'Yes' : 'No';
  if (type === 'date') return displayDate(v);
  return String(v);
}

/** A declared field's value on the record page: yes/no for a switch, null when not set. */
export function detailText(type: string, v: unknown): string | null {
  if (blank(v)) return null;
  return type === 'bool' ? (v ? 'Yes' : 'No') : String(v);
}
