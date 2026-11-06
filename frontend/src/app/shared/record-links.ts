/**
 * Where a record type from the audit trail lives in the app. A type missing here has no page of its own, so the audit log simply offers
 * no link. The names are the record classes the audit trail stores; the module routes are the ones app.routes.ts already lists.
 */
const ROUTES: Record<string, (id: string, kind?: string | null) => unknown[]> = {
  Party: (id) => ['/parties', id],
  Invoice: (id) => ['/finance/invoices', id],
  Bill: (id) => ['/finance/bills', id],
  JournalEntry: (id) => ['/finance/journals', id],
  // One table holds both directions of money; the snapshot says which (0 or "Received" is money in).
  Settlement: (id, kind) => [kind === '1' || kind === 'Issued' ? '/finance/payments' : '/finance/receipts', id],
  SalesQuote: (id) => ['/sales/quotes', id],
  SalesOrder: (id) => ['/sales/orders', id],
  PurchaseOrder: (id) => ['/purchasing/orders', id],
  PosOrder: (id) => ['/pos/orders', id],
  Project: (id) => ['/projects', id],
  StockCount: (id) => ['/inventory/counts', id],
  PayrollRun: (id) => ['/hr/payroll', id],
  Property: (id) => ['/real-estate/properties', id],
  Lease: (id) => ['/real-estate/leases', id],
  Acquisition: (id) => ['/real-estate/acquisitions', id],
};

export function recordLink(recordType: string, recordId: string, kind?: string | null): unknown[] | null {
  return ROUTES[recordType]?.(recordId, kind) ?? null;
}
