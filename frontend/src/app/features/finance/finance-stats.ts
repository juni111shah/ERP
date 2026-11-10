import { Observable, forkJoin } from 'rxjs';
import { BillSummary, InvoiceSummary, PaymentSummary, ReceiptSummary } from '@core/api/admin.api';
import { daysOverdue } from './finance-format';
import { AllRows, Source, countOf, countRows, fetchAllRows, moneyByCurrency, startOfMonth, startOfYear } from '@shared/utils/kpi-stats';

/** The KPI cards of the invoice, bill, receipt and payment lists: what is read for them and how the figures are worded. */

/** Open documents and this year's documents, read in full, and the number of drafts. */
export interface DocumentStatsData<T> { open: AllRows<T>; year: AllRows<T>; drafts: number; }

/** Reads the figures behind the invoice or bill cards; `dateField` is the document date the year starts from. */
export function loadDocumentStats<T>(source: Source<T>, dateField: 'invoiceDate' | 'billDate'): Observable<DocumentStatsData<T>> {
  return forkJoin({
    open: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid'] }], 'dueDate'),
    year: fetchAllRows(source, [{ field: 'status', op: 'in', value: ['issued', 'partlyPaid', 'paid'] }, { field: dateField, op: 'gte', value: startOfYear() }], `-${dateField}`),
    drafts: countRows(source, [{ field: 'status', op: 'eq', value: 'draft' }]),
  });
}

/** Every posted receipt or payment, newest first. */
export function loadSettlementStats<T>(source: Source<T>): Observable<AllRows<T>> {
  return fetchAllRows(source, [{ field: 'status', op: 'eq', value: 'issued' }], '-date');
}

/** The invoice cards: invoiced this year less credit notes, outstanding, overdue and drafts. */
export function invoiceStats(d: DocumentStatsData<InvoiceSummary>, currency: string) {
  const open = d.open.items.filter((r) => !r.isCreditNote);
  const credits = d.open.items.filter((r) => r.isCreditNote);
  const late = open.filter((r) => daysOverdue(r) > 0);
  const yearInvoices = d.year.items.filter((r) => !r.isCreditNote).length;
  const partial = d.open.capped || d.year.capped ? ' (latest rows only)' : '';
  return {
    invoiced: moneyByCurrency(d.year.items, (r) => (r.isCreditNote ? -r.total : r.total), currency),
    invoicedCaption: `${countOf(yearInvoices, 'invoice')}, less credit notes${partial}`,
    outstanding: moneyByCurrency(open, (r) => r.outstanding, currency),
    outstandingCaption: credits.length
      ? `${open.length} open · ${moneyByCurrency(credits, (r) => r.outstanding, currency)} in credits`
      : countOf(open.length, 'open invoice'),
    overdue: moneyByCurrency(late, (r) => r.outstanding, currency),
    overdueCount: late.length,
    overdueCaption: late.length ? countOf(late.length, 'invoice') : 'Nothing overdue',
    drafts: d.drafts,
  };
}

/** The bill cards: billed this year, outstanding, overdue and drafts. */
export function billStats(d: DocumentStatsData<BillSummary>, currency: string) {
  const late = d.open.items.filter((b) => daysOverdue(b) > 0);
  return {
    billed: moneyByCurrency(d.year.items, (b) => b.total, currency),
    billedCaption: countOf(d.year.items.length, 'bill') + (d.year.capped ? ' (latest rows only)' : ''),
    outstanding: moneyByCurrency(d.open.items, (b) => b.outstanding, currency),
    outstandingCaption: d.open.items.length ? countOf(d.open.items.length, 'open bill') : 'Everything is paid',
    overdue: moneyByCurrency(late, (b) => b.outstanding, currency),
    overdueCount: late.length,
    overdueCaption: late.length ? countOf(late.length, 'bill') : 'Nothing overdue',
    drafts: d.drafts,
  };
}

/**
 * The receipt or payment cards: this month, this year and what is not allocated yet.
 * `noun` names one document ("receipt"); `unallocatedLabel` opens the caption of the unallocated card ("On account").
 */
export function settlementStats(d: AllRows<ReceiptSummary | PaymentSummary>, currency: string, noun: string, unallocatedLabel: string) {
  const month = d.items.filter((r) => r.date >= startOfMonth());
  const year = d.items.filter((r) => r.date >= startOfYear());
  const open = d.items.filter((r) => r.unallocated > 0);
  return {
    count: d.total,
    month: moneyByCurrency(month, (r) => r.amount, currency),
    monthCaption: countOf(month.length, noun),
    year: moneyByCurrency(year, (r) => r.amount, currency),
    yearCaption: countOf(year.length, noun),
    unallocated: moneyByCurrency(open, (r) => r.unallocated, currency),
    unallocatedCount: open.length,
    unallocatedCaption: (open.length ? `${unallocatedLabel}, ${countOf(open.length, noun)}` : 'All allocated') + (d.capped ? ' (latest rows only)' : ''),
  };
}
