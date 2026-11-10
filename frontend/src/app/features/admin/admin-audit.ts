import { AuditEvent } from '@core/api/admin.api';

/** The time ranges the audit log can be narrowed to. */
export type AuditRange = 'all' | 'today' | '7' | '30';

/** The start of a range as a timestamp: local midnight today, or that many days back counting today; null for all time. */
export function auditRangeStart(key: AuditRange, now = new Date()): string | null {
  if (key === 'all') return null;
  const start = new Date(now);
  start.setDate(start.getDate() - (key === 'today' ? 0 : Number(key) - 1));
  start.setHours(0, 0, 0, 0);
  return start.toISOString();
}

/** One line of the audit CSV export. */
export interface AuditCsvRow { occurredAt: string; by: string; action: string; recordType: string; record: string; recordId: string; changedFields: string; ipAddress: string; }

/** The columns of the audit CSV export. */
export const AUDIT_CSV_COLUMNS: { field: keyof AuditCsvRow; header: string }[] = [
  { field: 'occurredAt', header: 'When (UTC)' }, { field: 'by', header: 'By' }, { field: 'action', header: 'Action' }, { field: 'recordType', header: 'Record type' },
  { field: 'record', header: 'Record' }, { field: 'recordId', header: 'Record ID' }, { field: 'changedFields', header: 'Changed fields' }, { field: 'ipAddress', header: 'IP address' },
];

/** An event as a CSV line; `who` names the person (or the system) behind it. */
export function auditCsvRow(e: AuditEvent, who: string): AuditCsvRow {
  return {
    occurredAt: e.occurredAt, by: who, action: e.action, recordType: e.recordType, record: e.recordLabel ?? '', recordId: e.recordId, changedFields: e.changedFields.join(', '), ipAddress: e.ipAddress ?? '',
  };
}
