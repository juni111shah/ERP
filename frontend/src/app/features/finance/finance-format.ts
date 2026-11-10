/** How finance documents read on screen: money, document status and source labels, tax names per country, days overdue. */

/** Tax label per country, same table as the print layout; anything else is plain "Tax". */
const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };

export function taxNameFor(country?: string | null): string { return TAX_NAME[(country ?? '').toUpperCase()] ?? 'Tax'; }

/** The name of the tax registration number in a country (TRN, GSTIN, NTN...). */
export function taxRegLabelFor(country?: string | null): string {
  const c = (country ?? '').toUpperCase();
  return c === 'AE' || c === 'SA' ? 'TRN' : c === 'IN' ? 'GSTIN' : c === 'PK' ? 'NTN' : c === 'GB' ? 'VAT no.' : 'Tax reg. no.';
}

const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Pending approval' };

/** "Part paid", "Pending approval", otherwise the status with a capital first letter. */
export function invoiceStatusLabel(s: string): string { return STATUS_LABELS[s] ?? (s ? s.charAt(0).toUpperCase() + s.slice(1) : ''); }

/** Friendly names for module source keys ("<module>.<kind>"); anything else is humanized below. */
const SOURCE_LABELS: Record<string, string> = { 'realestate.instalment': 'Rent instalment' };

/** The document's source as a label ("Rent instalment", "Sales order"); empty for manual documents. */
export function humanizeSource(v?: string | null): string {
  if (!v || v.toLowerCase() === 'manual') return '';
  if (SOURCE_LABELS[v.toLowerCase()]) return SOURCE_LABELS[v.toLowerCase()];
  const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._-]+/g, ' ');
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}

/** "1,234.50", or "QAR 1,234.50" with a currency. */
export function formatMoney(v: number | null | undefined, currency?: string | null): string {
  const n = (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${currency} ${n}` : n;
}

/** Whole days past the due date for an open invoice or bill; 0 when not overdue. A credit note is never overdue: it is money owed to the customer. */
export function daysOverdue(inv: { dueDate: string; status: string; total: number; paid: number; isCreditNote?: boolean }): number {
  if (inv.isCreditNote || !(inv.status === 'issued' || inv.status === 'partlyPaid') || inv.total - inv.paid <= 0) return 0;
  const due = new Date(inv.dueDate + 'T00:00:00');
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const diff = Math.floor((today.getTime() - due.getTime()) / 86400000);
  return diff > 0 ? diff : 0;
}

/** An issued document that still has something to pay (or to apply). */
export const isOpenStatus = (status: string): boolean => status === 'issued' || status === 'partlyPaid';
