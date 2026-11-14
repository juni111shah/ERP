/**
 * The read permission of the core pages a dashboard card or alert can link to (the route guards in app.routes.ts). The server only sends
 * a card when its figures may be seen, but a card's link can still point at a page this person may not open (cash on the owner view
 * links to the bank page, "collected" to receipts), so such a link is dropped. Module pages guard themselves and are not listed.
 */
const LINK_PERMISSIONS: [prefix: string, permission: string][] = [
  ['/finance/invoices', 'finance.invoices.read'],
  ['/finance/receipts', 'finance.receipts.read'],
  ['/finance/bills', 'finance.bills.read'],
  ['/finance/payments', 'finance.payments.read'],
  ['/finance/journals', 'finance.journals.read'],
  ['/finance/accounts', 'finance.accounts.read'],
  ['/finance/budgets', 'finance.budgets.read'],
  ['/finance/cheques', 'finance.cheques.read'],
  ['/finance/bank', 'finance.bank.read'],
  ['/finance/reports', 'finance.reports.read'],
  ['/finance/tax-codes', 'finance.tax.read'],
  ['/parties', 'core.parties.read'],
  ['/catalog/items', 'core.items.read'],
  ['/admin/audit', 'core.audit.read'],
];

/** The link when this person may open it, else null (the card then shows without a link). */
export function allowedLink(link: string | null | undefined, hasPermission: (permission: string) => boolean): string | null {
  if (!link) return null;
  const path = link.split(/[?#]/)[0];
  const rule = LINK_PERMISSIONS.find(([prefix]) => path === prefix || path.startsWith(prefix + '/'));
  return !rule || hasPermission(rule[1]) ? link : null;
}
