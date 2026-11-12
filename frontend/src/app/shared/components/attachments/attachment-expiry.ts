import { toIsoDate } from '@core/format/dates';

/** Days until a date (negative = past); null when there is no date. */
export function daysUntil(date?: string | null): number | null {
  if (!date) return null;
  const [y, m, d] = date.split('-').map(Number);
  return Math.round((new Date(y, m - 1, d).getTime() - new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate()).getTime()) / 86400000);
}

/** Tag colour of a document's expiry date: red once expired, orange within 30 days, else plain. */
export function expiryTone(date: string): string {
  const d = daysUntil(date) ?? 0;
  return d < 0 ? 'red' : d <= 30 ? 'orange' : 'default';
}

/** The upload form of one file for a record, with the optional category and expiry day. */
export function attachmentUploadForm(ownerType: string, ownerId: string, file: File, category: string, expires: Date | null): FormData {
  const form = new FormData();
  form.append('ownerType', ownerType);
  form.append('ownerId', ownerId);
  form.append('file', file);
  if (category.trim()) form.append('category', category.trim());
  if (expires) form.append('expiresOn', toIsoDate(expires));
  return form;
}
