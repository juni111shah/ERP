import { AuditEvent } from '@core/api/admin.api';

/**
 * The read permission of the page each audited record type opens (the route guard of that page). The audit log offers "Open record"
 * only to people who hold it; a type not listed here has no extra check.
 */
const RECORD_READ_PERMISSIONS: Record<string, (kind?: string | null) => string> = {
  Party: () => 'core.parties.read',
  Invoice: () => 'finance.invoices.read',
  Bill: () => 'finance.bills.read',
  JournalEntry: () => 'finance.journals.read',
  Settlement: (kind) => (kind === '1' || kind === 'Issued' ? 'finance.payments.read' : 'finance.receipts.read'),
  SalesQuote: () => 'sales.quotes.read',
  SalesOrder: () => 'sales.orders.read',
  PurchaseOrder: () => 'purchasing.orders.read',
  PosOrder: () => 'pos.orders.read',
  Project: () => 'projects.projects.read',
  StockCount: () => 'inventory.counts.read',
  PayrollRun: () => 'hr.payroll.read',
  Property: () => 'realestate.properties.read',
  Lease: () => 'realestate.leases.read',
  Acquisition: () => 'realestate.acquisitions.read',
};

/** The permission needed to open the page of an audited record, or null when the page has no check of its own. */
export function auditRecordPermission(recordType: string, kind?: string | null): string | null {
  return RECORD_READ_PERMISSIONS[recordType]?.(kind) ?? null;
}

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
