import { displayLabel } from '../format/display';

/** The title bar's text for an address: the page title, an optional subtitle and the module it belongs to. */
export interface RouteTitle { title: string; subtitle?: string; module?: string; }

/** Looks a dictionary key up in the current language, falling back to the English text. */
export type TitleTranslator = (key: string, fallback: string) => string;

/** A title, translated when it has a dictionary key. */
interface TitleEntry { title: string; titleKey?: string; module?: string; }

const OVERVIEW_PATHS = ['', '/', '/dashboard'];

/** /parties?type=…: customers and vendors are the same screen under their own menu entries. */
const PARTY_TITLES = new Map<string, TitleEntry>([
  ['customer', { titleKey: 'menu.customers', title: 'Customers', module: 'Sales' }],
  ['vendor', { titleKey: 'menu.vendors', title: 'Vendors', module: 'Purchasing' }],
]);
const ALL_PARTIES: TitleEntry = { titleKey: 'menu.parties', title: 'Parties', module: 'Core' };

/** Exact addresses. */
const ROUTE_TITLES: Record<string, TitleEntry> = {
  // Finance & Accounting
  '/finance/accounts': { titleKey: 'coa.title', title: 'Chart of accounts', module: 'Finance' },
  '/finance/budgets': { title: 'Budgets', module: 'Finance' },
  '/finance/journals': { title: 'Journals', module: 'Finance' },
  '/finance/reports': { title: 'Financial reports', module: 'Finance' },
  '/finance/invoices': { title: 'Invoices', module: 'Finance' },
  '/finance/receipts': { title: 'Receipts', module: 'Finance' },
  '/finance/cheques': { title: 'Cheque register', module: 'Finance' },
  '/finance/bank': { title: 'Cash and bank', module: 'Finance' },
  '/finance/bills': { title: 'Bills', module: 'Finance' },
  '/finance/payments': { title: 'Payments', module: 'Finance' },
  '/finance/tax-codes': { title: 'Tax codes', module: 'Finance' },

  // Platform & Administration
  '/catalog/items': { title: 'Items', module: 'Catalog' },
  '/admin/custom-fields': { title: 'Custom fields', module: 'Platform' },
  '/admin/modules': { title: 'Modules', module: 'Platform' },
  '/admin/approval-rules': { title: 'Approval rules', module: 'Platform' },
  '/admin/legal-entities': { title: 'Legal entities', module: 'Platform' },
  '/admin/business-units': { title: 'Business units', module: 'Platform' },
  '/admin/users': { title: 'Users', module: 'Platform' },
  '/admin/roles': { title: 'Roles', module: 'Platform' },
  '/admin/settings': { title: 'Settings', module: 'Platform' },
  '/admin/import': { title: 'Data import', module: 'Platform' },
  '/admin/documents': { title: 'Document vault', module: 'Platform' },
  '/admin/messages': { title: 'Message log', module: 'Platform' },
  '/admin/audit': { title: 'Audit log', module: 'Platform' },
  '/admin/billing': { title: 'Subscription and billing', module: 'Platform' },
  '/approvals': { title: 'Approvals', module: 'Platform' },

  // The founder's console
  '/platform': { title: 'Dashboard', module: 'Console' },
  '/platform/clients': { title: 'Clients', module: 'Console' },
  '/platform/signups': { title: 'Registrations', module: 'Console' },
  '/platform/leads': { title: 'Leads', module: 'Console' },
  '/platform/plans': { title: 'Plans and pricing', module: 'Console' },
  '/platform/invoices': { title: 'Invoices', module: 'Console' },
  '/platform/activity': { title: 'Activity', module: 'Console' },
  '/platform/team': { title: 'Team', module: 'Console' },
  '/platform/assistant': { title: 'AI assistant', module: 'Console' },
  '/platform/settings': { title: 'Settings', module: 'Console' },
  '/account/security': { titleKey: 'security.title', title: 'Account security', module: 'Account' },
  '/reports': { title: 'Reports', module: 'Reports' },

  // Inventory
  '/inventory/stock': { title: 'Stock Balance', module: 'Inventory' },
  '/inventory/movements': { title: 'Stock Movements', module: 'Inventory' },
  '/inventory/warehouses': { title: 'Warehouses', module: 'Inventory' },

  // HR
  '/hr/employees': { title: 'Employees', module: 'HR' },
  '/hr/my': { title: 'My HR', module: 'HR' },
  '/hr/attendance': { title: 'Attendance', module: 'HR' },
  '/hr/payroll': { title: 'Payroll', module: 'HR' },
  '/hr/reviews': { title: 'Performance Reviews', module: 'HR' },
  '/hr/review-cycles': { title: 'Review Cycles', module: 'HR' },

  // Real Estate
  '/real-estate/properties': { title: 'Properties', module: 'Real Estate' },
  '/real-estate/units': { title: 'Units', module: 'Real Estate' },
  '/real-estate/unit-types': { title: 'Unit Types', module: 'Real Estate' },
  '/real-estate/leases': { title: 'Leases', module: 'Real Estate' },
  '/real-estate/deals': { title: 'Deals', module: 'Real Estate' },
  '/real-estate/listings': { title: 'Listings', module: 'Real Estate' },
  '/real-estate/leads': { title: 'Leads', module: 'Real Estate' },
  '/real-estate/inspections': { title: 'Inspections', module: 'Real Estate' },
  '/real-estate/pm-schedules': { title: 'PM Schedules', module: 'Real Estate' },
  '/real-estate/work-orders': { title: 'Work Orders', module: 'Real Estate' },
  '/real-estate/acquisitions': { title: 'Acquisitions and funding', module: 'Real Estate' },

  // Projects
  '/projects/list': { title: 'Projects', module: 'Projects' },
  '/projects/tasks': { title: 'Tasks', module: 'Projects' },
  '/projects/timesheets': { title: 'Timesheets', module: 'Projects' },
  '/projects/billing': { title: 'Project billing', module: 'Projects' },
  '/projects/board': { title: 'Task board', module: 'Projects' },
  '/projects/week': { title: 'My week', module: 'Projects' },
  '/projects/expenses': { title: 'Expenses', module: 'Projects' },
  '/projects/approvals': { title: 'Approvals', module: 'Projects' },
  '/projects/reports': { title: 'Project reports', module: 'Projects' },
  '/projects/resources': { title: 'Resources', module: 'Projects' },
  '/projects/change-orders': { title: 'Change orders', module: 'Projects' },
  '/projects/risks': { title: 'Risks and issues', module: 'Projects' },

  // Sales
  '/sales/quotes': { title: 'Sales Quotes', module: 'Sales' },
  '/sales/orders': { title: 'Sales Orders', module: 'Sales' },
  '/sales/analytics': { title: 'Sales Analytics', module: 'Sales' },

  // Purchasing
  '/purchasing/orders': { title: 'Purchase Orders', module: 'Purchasing' },
  '/purchasing/requisitions': { title: 'Purchase Requisitions', module: 'Purchasing' },
  '/purchasing/receipts': { title: 'Goods Receipts (GRN)', module: 'Purchasing' },
  '/purchasing/analytics': { title: 'Purchasing Analytics', module: 'Purchasing' },

  // POS
  '/pos/terminal': { title: 'Point of Sale', module: 'POS' },
  '/pos/sessions': { title: 'Shifts & Cash Drawers', module: 'POS' },
  '/pos/orders': { title: 'Counter Sales Orders', module: 'POS' },
  '/pos/returns': { title: 'Returns', module: 'POS' },
  '/pos/registers': { title: 'Point of Sale Registers', module: 'POS' },
  '/pos/analytics': { title: 'Point of Sale Analytics', module: 'POS' },

  // Retail
  '/retail/stores': { title: 'Retail Stores', module: 'Retail' },
  '/retail/pricelists': { title: 'Retail Price Lists', module: 'Retail' },
  '/retail/promotions': { title: 'Promotions & Discounts', module: 'Retail' },
  '/retail/analytics': { title: 'Retail Analytics', module: 'Retail' },

  // Distribution
  '/distribution/routes': { title: 'Delivery Routes', module: 'Distribution' },
  '/distribution/trips': { title: 'Van Sales Trips', module: 'Distribution' },
  '/distribution/wholesale': { title: 'Wholesale Price Matrices', module: 'Distribution' },
  '/distribution/analytics': { title: 'Distribution Analytics', module: 'Distribution' },

  // Manufacturing
  '/manufacturing/boms': { title: 'Bills of Materials (BOM)', module: 'Manufacturing' },
  '/manufacturing/workorders': { title: 'Manufacturing Work Orders', module: 'Manufacturing' },
  '/manufacturing/costing': { title: 'Product Costing', module: 'Manufacturing' },
  '/manufacturing/analytics': { title: 'Manufacturing Analytics', module: 'Manufacturing' },

  // CRM
  '/crm/leads': { title: 'CRM Leads', module: 'CRM' },
  '/crm/opportunities': { title: 'Opportunities Pipeline', module: 'CRM' },
  '/crm/interactions': { title: 'Customer Interactions', module: 'CRM' },
  '/crm/analytics': { title: 'CRM Analytics', module: 'CRM' },

  // Services
  '/services/engagements': { title: 'Client Engagements', module: 'Services' },
  '/services/retainers': { title: 'Advisory Retainers', module: 'Services' },
  '/services/milestones': { title: 'Milestone Deliverables & Billing', module: 'Services' },
  '/services/analytics': { title: 'Services Analytics', module: 'Services' },
};

/** Record pages below a list (a record id or a report key after the prefix), checked in this order. */
const DETAIL_TITLES: readonly (readonly [prefix: string, title: string, module: string])[] = [
  ['/finance/invoices/', 'Invoice Details', 'Finance'],
  ['/finance/journals/', 'Journal Details', 'Finance'],
  ['/finance/receipts/', 'Receipt Details', 'Finance'],
  ['/finance/bills/', 'Bill Details', 'Finance'],
  ['/finance/payments/', 'Payment Details', 'Finance'],
  ['/parties/', 'Party Details', 'Core'],
];
const DETAIL_TITLES_AFTER_REPORTS: readonly (readonly [prefix: string, title: string, module: string])[] = [
  ['/sales/quotes/', 'Quote Details', 'Sales'],
  ['/sales/orders/', 'Order Details', 'Sales'],
  ['/purchasing/orders/', 'Purchase Order Details', 'Purchasing'],
  ['/projects/', 'Project Details', 'Projects'],
  ['/real-estate/properties/', 'Property Details', 'Real Estate'],
  ['/real-estate/leases/', 'Lease Details', 'Real Estate'],
  ['/hr/payroll/', 'Payroll Run Details', 'HR'],
];

const RECORD_ID = /^[0-9a-f]{8}-[0-9a-f]{4}-/i;

/** The title of /parties, which depends on the `type` query parameter. */
export function partiesTitle(type: string | null, t: TitleTranslator): RouteTitle {
  return translated(PARTY_TITLES.get(type ?? '') ?? ALL_PARTIES, t);
}

/** The title for a path without its query: an exact entry, a record page under a known list, else words made from the address. */
export function routeTitle(path: string, t: TitleTranslator): RouteTitle | null {
  if (OVERVIEW_PATHS.includes(path)) return { title: t('menu.overview', 'Overview'), module: 'Workspace' };
  const exact = ROUTE_TITLES[path];
  if (exact) return translated(exact, t);
  const detail = detailTitle(path);
  if (detail) return detail;
  return fallbackTitle(path);
}

function detailTitle(path: string): RouteTitle | null {
  const before = DETAIL_TITLES.find(([prefix]) => path.startsWith(prefix));
  if (before) return { title: before[1], module: before[2] };
  if (path.startsWith('/reports/')) return { title: displayLabel(path.split('/')[2]) || 'Report', module: 'Reports' };
  const after = DETAIL_TITLES_AFTER_REPORTS.find(([prefix]) => path.startsWith(prefix));
  return after ? { title: after[1], module: after[2] } : null;
}

/** "/things/<id>" reads "Thing details"; any other address reads as its last segment. */
function fallbackTitle(path: string): RouteTitle | null {
  const segs = path.split('/').filter(Boolean);
  if (segs.length === 0) return null;
  const last = segs[segs.length - 1];
  if (RECORD_ID.test(last) && segs.length > 1) {
    const parent = segs[segs.length - 2].replace(/-/g, ' ').replace(/ies$/, 'y').replace(/s$/, '');
    return { title: `${parent.charAt(0).toUpperCase()}${parent.slice(1)} details` };
  }
  return { title: last.charAt(0).toUpperCase() + last.slice(1).replace(/-/g, ' ') };
}

function translated(entry: TitleEntry, t: TitleTranslator): RouteTitle {
  return { title: entry.titleKey ? t(entry.titleKey, entry.title) : entry.title, module: entry.module };
}
