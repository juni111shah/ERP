/** One thing the "New" menu can start: where it goes, and what the workspace must have for it to make sense. */
export interface QuickCreateEntry {
  link: string;
  query?: Record<string, string>;
  icon: string;
  color: string;
  labelKey: string;
  label: string;
  /** The module the destination belongs to; the entry is hidden while it is off. Core destinations leave it out. */
  module?: string;
  /** The permission that lets the person create this; the entry is hidden without it. */
  permission: string;
}
export interface QuickCreateGroup { titleKey: string; title: string; entries: QuickCreateEntry[]; }

/**
 * Starting points of the "New" menu: core finance and master data, and the commercial modules every workspace may switch on.
 * A business-specific record type is created from its own page, so none is named here.
 */
export const QUICK_CREATE: QuickCreateGroup[] = [
  { titleKey: 'menu.group.sales', title: 'Sales', entries: [
    { link: '/finance/invoices', icon: 'file-text', color: 'green', labelKey: 'menu.finance.invoices', label: 'Invoice', permission: 'finance.invoices.write' },
    { link: '/sales/quotes', icon: 'file-done', color: 'green', labelKey: 'menu.sales.quotes', label: 'Quotation', module: 'sales', permission: 'sales.quotes.manage' },
    { link: '/sales/orders', icon: 'shopping-cart', color: 'green', labelKey: 'menu.sales.orders', label: 'Sales Order', module: 'sales', permission: 'sales.orders.manage' },
    { link: '/parties', query: { type: 'customer' }, icon: 'user-add', color: 'blue', labelKey: 'menu.sales.customers', label: 'Customer', permission: 'core.parties.write' },
  ] },
  { titleKey: 'menu.group.purchasing', title: 'Purchasing', entries: [
    { link: '/purchasing/orders', icon: 'shopping', color: 'orange', labelKey: 'menu.purchasing.orders', label: 'Purchase Order', module: 'purchasing', permission: 'purchasing.orders.manage' },
    { link: '/finance/bills', icon: 'audit', color: 'orange', labelKey: 'menu.finance.bills', label: 'Vendor Bill', permission: 'finance.bills.write' },
    { link: '/purchasing/receipts', icon: 'inbox', color: 'orange', labelKey: 'menu.purchasing.receipts', label: 'Goods Receipt', module: 'purchasing', permission: 'purchasing.receipts.manage' },
    { link: '/parties', query: { type: 'vendor' }, icon: 'team', color: 'blue', labelKey: 'menu.purchases.vendors', label: 'Vendor', permission: 'core.parties.write' },
  ] },
  { titleKey: 'menu.group.finance', title: 'Finance', entries: [
    { link: '/finance/journals', icon: 'book', color: 'purple', labelKey: 'menu.finance.journals', label: 'Journal Entry', permission: 'finance.journals.write' },
    { link: '/finance/receipts', icon: 'dollar-circle', color: 'green', labelKey: 'menu.finance.receipts', label: 'Receive Money', permission: 'finance.receipts.write' },
    { link: '/finance/payments', icon: 'transaction', color: 'red', labelKey: 'menu.finance.payments', label: 'Make Payment', permission: 'finance.payments.write' },
    { link: '/finance/accounts', icon: 'apartment', color: 'purple', labelKey: 'menu.finance.accounts', label: 'Account', permission: 'finance.accounts.write' },
  ] },
  { titleKey: 'menu.group.masterdata', title: 'Catalog', entries: [
    { link: '/catalog/items', icon: 'barcode', color: 'teal', labelKey: 'menu.core.items', label: 'Item / Product', permission: 'core.items.write' },
    { link: '/pos/terminal', icon: 'calculator', color: 'teal', labelKey: 'menu.pos.terminal', label: 'POS Terminal', module: 'pos', permission: 'pos.orders.create' },
  ] },
];

/** The menu for this person and workspace: only entries whose module is on and whose permission is held, and no empty groups. */
export function visibleQuickCreate(isModuleEnabled: (key: string) => boolean, hasPermission: (permission: string) => boolean, groups = QUICK_CREATE): QuickCreateGroup[] {
  return groups
    .map((g) => ({ ...g, entries: g.entries.filter((e) => (!e.module || isModuleEnabled(e.module)) && hasPermission(e.permission)) }))
    .filter((g) => g.entries.length > 0);
}
