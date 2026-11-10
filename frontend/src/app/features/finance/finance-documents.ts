import { KpiCard } from '@shared/components/detail-page/detail-page.component';
import { formatMoney } from './finance-format';

/** Shared parts of the invoice, bill and journal detail pages. */

/** What is still to pay on an invoice or bill (nothing once voided). */
export function balanceDue(doc: { status: string; total: number; paid: number }): number {
  return doc.status !== 'voided' ? doc.total - doc.paid : 0;
}

/** Total, paid, balance due (red while something is due) and days overdue (red once late). */
export function paymentKpis(doc: { total: number; paid: number; currencyCode: string }, balance: number, overdue: number): KpiCard[] {
  return [
    { title: 'Total Amount', value: formatMoney(doc.total), suffix: doc.currencyCode },
    { title: 'Amount Paid', value: formatMoney(doc.paid), suffix: doc.currencyCode },
    { title: 'Balance Due', value: formatMoney(balance), suffix: doc.currencyCode, valueStyle: { color: balance > 0 ? '#ff4d4f' : '#52c41a' } },
    { title: 'Days Overdue', value: overdue || 0, valueStyle: { color: overdue > 0 ? '#ff4d4f' : '#52c41a' } },
  ];
}

/** Opens the print layout of a document in a new tab. */
export function printDocument(kind: 'invoice' | 'bill' | 'receipt' | 'payment' | 'journal', id: string): void {
  window.open(`/print/${kind}/${id}`, '_blank');
}

/** The page of the document a journal came from, by the journal's source type. */
const JOURNAL_SOURCE_ROUTES: Record<string, string> = {
  invoice: '/finance/invoices',
  receipt: '/finance/receipts',
  bill: '/finance/bills',
  payment: '/finance/payments',
  cheque: '/finance/cheques',
  pos: '/pos/orders',
  posorder: '/pos/orders',
  'pos-order': '/pos/orders',
  order: '/sales/orders',
  sales: '/sales/orders',
  salesorder: '/sales/orders',
  quote: '/sales/quotes',
  purchase: '/purchasing/orders',
  purchaseorder: '/purchasing/orders',
  purchasereceipt: '/purchasing/receipts',
  movement: '/inventory/movements',
  stock: '/inventory/stock',
  workorder: '/manufacturing/workorders',
  payroll: '/hr/payroll',
  lease: '/real-estate/leases',
};

const EMPTY_ID = '00000000-0000-0000-0000-000000000000';

/** The route of a journal's source document (its list when the id is empty), or null when the source has no page. */
export function journalSourceLink(sourceType: string, sourceId: string): string[] | null {
  const basePath = JOURNAL_SOURCE_ROUTES[(sourceType ?? '').toLowerCase().trim()];
  if (!basePath) return null;
  return sourceId && sourceId !== EMPTY_ID ? [basePath, sourceId] : [basePath];
}
