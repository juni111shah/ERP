import { BillSummary, InvoiceSummary } from '@core/api/admin.api';
import { PartyDocumentList, PartyDocumentRow } from '@core/api/party-activity.api';
import { KpiCard } from '@shared/components/detail-page/detail-page.component';
import { AllRows, moneyByCurrency } from '@shared/utils/kpi-stats';
import { isOpenStatus } from '@features/finance/finance-format';

/** The party detail page's activity: its latest documents in one table and the sales / purchases cards. */

/** One row of the party's recent activity. */
export interface Transaction {
  id: string;
  type: string;
  number: string;
  date: string;
  amount: number;
  outstanding: number | null;
  status: string;
  currencyCode: string;
}

const TYPE_OF: Record<PartyDocumentList, string> = { invoices: 'Invoice', receipts: 'Receipt', bills: 'Bill', payments: 'Payment' };

/** The page of each transaction type. */
const TRANSACTION_ROUTES: Record<string, string> = {
  'Invoice': '/finance/invoices',
  'Credit note': '/finance/invoices',
  'Receipt': '/finance/receipts',
  'Bill': '/finance/bills',
  'Payment': '/finance/payments',
};

/** The read permission of each transaction type's page (its route guard and GET endpoint). */
const TRANSACTION_READ: Record<string, string> = {
  'Invoice': 'finance.invoices.read',
  'Credit note': 'finance.invoices.read',
  'Receipt': 'finance.receipts.read',
  'Bill': 'finance.bills.read',
  'Payment': 'finance.payments.read',
};

/** The permission needed to open a transaction's document page. */
export function transactionPermission(tx: Transaction): string | undefined {
  return TRANSACTION_READ[tx.type];
}

/** The lists to read: invoices and receipts of a customer, bills and payments of a vendor. */
export function activityLists(isCustomer: boolean, isVendor: boolean): PartyDocumentList[] {
  return [...(isCustomer ? ['invoices', 'receipts'] as const : []), ...(isVendor ? ['bills', 'payments'] as const : [])];
}

/** A document list row as an activity row. */
export function toTransaction(item: PartyDocumentRow, list: PartyDocumentList): Transaction {
  // A credit note takes money off the account, so it reads as negative here too.
  const sign = item.isCreditNote ? -1 : 1;
  return {
    id: item.id,
    type: item.isCreditNote ? 'Credit note' : TYPE_OF[list],
    number: item.number,
    date: (item.invoiceDate || item.billDate || item.date)!,
    amount: sign * (item.total || item.amount || 0),
    // Receipts and payments have no balance of their own.
    outstanding: item.total === undefined ? null : item.status === 'voided' ? 0 : sign * (item.total - (item.paid || 0)),
    status: item.total === undefined && item.status === 'issued' ? 'posted' : item.status,
    currencyCode: item.currencyCode,
  };
}

/** The 20 latest rows of all lists, newest first. */
export function latestTransactions(responses: ({ items?: PartyDocumentRow[] } | null | undefined)[], lists: PartyDocumentList[]): Transaction[] {
  const all: Transaction[] = [];
  responses.forEach((res, idx) => { if (res?.items) res.items.forEach((item) => all.push(toTransaction(item, lists[idx]))); });
  all.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
  return all.slice(0, 20);
}

/** The route of a transaction's document. */
export function transactionLink(tx: Transaction): string[] {
  return [TRANSACTION_ROUTES[tx.type], tx.id];
}

/** Total sales (less credit notes) and the outstanding balance; a dash while the invoices are not read. */
export function customerCards(sales: AllRows<InvoiceSummary> | null, currency: string): KpiCard[] {
  const rows = sales?.items ?? [];
  const sign = (r: InvoiceSummary) => (r.isCreditNote ? -1 : 1);
  const open = rows.filter((r) => isOpenStatus(r.status));
  const balance = open.reduce((s, r) => s + sign(r) * r.outstanding, 0);
  return [
    { title: 'Total sales', caption: 'Less credit notes', value: sales ? moneyByCurrency(rows, (r) => sign(r) * r.total, currency) : '—' },
    { title: 'Outstanding', caption: balance < 0 ? 'Customer is in credit' : 'Open invoices less credits', value: sales ? moneyByCurrency(open, (r) => sign(r) * r.outstanding, currency) : '—', valueStyle: { color: balance > 0 ? '#fa8c16' : '#52c41a' } },
  ];
}

/** Total purchases and outstanding payables; a dash while the bills are not read. */
export function vendorCards(purchases: AllRows<BillSummary> | null, currency: string): KpiCard[] {
  const rows = purchases?.items ?? [];
  const open = rows.filter((r) => isOpenStatus(r.status));
  const balance = open.reduce((s, r) => s + r.outstanding, 0);
  return [
    { title: 'Total purchases', value: purchases ? moneyByCurrency(rows, (r) => r.total, currency) : '—' },
    { title: 'Outstanding payables', value: purchases ? moneyByCurrency(open, (r) => r.outstanding, currency) : '—', valueStyle: { color: balance > 0 ? '#fa8c16' : '#52c41a' } },
  ];
}
