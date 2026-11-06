import { displayDate, displayLabel, isIsoDate } from './display';
import { currentLang } from '../i18n/translate-now';

const SHOWN_FIELDS = 3;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Whether an event added, changed or removed something. The tones are the shared status tag colours. */
export function auditTone(action: string | null | undefined): 'success' | 'error' | 'info' {
  const a = (action ?? '').toLowerCase();
  if (a.startsWith('creat') || a.startsWith('insert')) return 'success';
  if (a.startsWith('delet')) return 'error';
  return 'info';
}

/** "WorkspaceAssistantEnabled" reads as "Workspace assistant enabled"; a reference field drops its Id. */
export function auditFieldLabel(field: string): string {
  return displayLabel(field.replace(/([a-z0-9])Id$/, '$1'));
}

/** A stored value in words: yes and no, the shared date format, a dash for nothing. Other values stay exactly as stored. */
export function auditValue(value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') return '—';
  if (value === 'True' || value === 'true') return 'Yes';
  if (value === 'False' || value === 'false') return 'No';
  if (isIsoDate(value)) return displayDate(value);
  if (GUID.test(value)) return value;
  return looksLikeJson(value) ? prettyJson(value) : value;
}

function looksLikeJson(value: string): boolean {
  const t = value.trim();
  return (t.startsWith('{') && t.endsWith('}')) || (t.startsWith('[') && t.endsWith(']'));
}

function prettyJson(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

/** One line for the table: the fields that moved, the first few by name and a count for the rest. */
export function auditSummary(e: { action: string; changedFields: readonly string[] }, t: (key: string, fallback: string) => string): string {
  if (e.action === 'Created') return t('ui.audit.newRecord', 'New record');
  if (e.action === 'Deleted') return t('ui.audit.removed', 'Removed');
  const fields = e.changedFields;
  if (!fields.length) return '';
  const named = fields.slice(0, SHOWN_FIELDS).map(auditFieldLabel).join(', ');
  return fields.length > SHOWN_FIELDS ? `${named} +${fields.length - SHOWN_FIELDS}` : named;
}

export function initials(name: string | null | undefined): string {
  const parts = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  return parts.length === 1 ? parts[0].slice(0, 2).toUpperCase() : (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** "5 minutes ago", in the current language. */
export function relativeTime(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000);
  const format = new Intl.RelativeTimeFormat(currentLang() === 'ar' ? 'ar' : 'en', { numeric: 'auto' });
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [['year', 31536000], ['month', 2592000], ['day', 86400], ['hour', 3600], ['minute', 60]];
  for (const [unit, size] of steps) if (Math.abs(seconds) >= size) return format.format(Math.trunc(seconds / size), unit);
  return format.format(seconds, 'second');
}
