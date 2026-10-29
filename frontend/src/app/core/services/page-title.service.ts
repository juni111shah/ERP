import { inject, Injectable, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { I18nService } from '../i18n/i18n.service';

export interface PageBadge {
  text: string;
  color?: string;
}

@Injectable({
  providedIn: 'root',
})
export class PageTitleService {
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  readonly title = signal<string>('Dashboard');
  readonly subtitle = signal<string | null>(null);
  readonly badge = signal<PageBadge | null>(null);
  readonly module = signal<string | null>(null);

  private customTitleUrl: string | null = null;

  constructor() {
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        if (this.customTitleUrl !== e.urlAfterRedirects) {
          this.resolveFromRoute(e.urlAfterRedirects);
        }
      });
  }

  /** Explicitly set or customize the page title (e.g. for dynamic record numbers) */
  setTitle(title: string, subtitle?: string | null, badge?: PageBadge | null, module?: string | null): void {
    this.title.set(title);
    this.subtitle.set(subtitle ?? null);
    this.badge.set(badge ?? null);
    if (module !== undefined) {
      this.module.set(module ?? null);
    }
    this.customTitleUrl = this.router.url;
  }

  private resolveFromRoute(url: string): void {
    const cleanUrl = url.split('?')[0].split('#')[0];
    const query = new URLSearchParams(url.includes('?') ? url.split('?')[1] : '');

    // Check query params for parties
    if (cleanUrl === '/parties') {
      const type = query.get('type');
      if (type === 'customer') {
        this.title.set(this.i18n.t('menu.customers', 'Customers'));
        this.module.set('Sales');
        this.subtitle.set(null);
        this.badge.set(null);
        return;
      }
      if (type === 'vendor') {
        this.title.set(this.i18n.t('menu.vendors', 'Vendors'));
        this.module.set('Purchasing');
        this.subtitle.set(null);
        this.badge.set(null);
        return;
      }
      this.title.set(this.i18n.t('menu.parties', 'Parties'));
      this.module.set('Core');
      this.subtitle.set(null);
      this.badge.set(null);
      return;
    }

    const matched = this.matchRouteTitle(cleanUrl);
    if (matched) {
      this.title.set(matched.title);
      this.subtitle.set(matched.subtitle ?? null);
      this.badge.set(null);
      this.module.set(matched.module ?? null);
    }
  }

  private matchRouteTitle(path: string): { title: string; subtitle?: string; module?: string } | null {
    if (path === '' || path === '/' || path === '/dashboard') {
      return { title: this.i18n.t('menu.dashboard', 'Dashboard'), module: 'Workspace' };
    }

    const ROUTE_MAP: Record<string, { title: string; module?: string }> = {
      // Finance & Accounting
      '/finance/accounts': { title: this.i18n.t('coa.title', 'Chart of accounts'), module: 'Finance' },
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
      '/approvals': { title: 'Approvals', module: 'Platform' },
      '/account/security': { title: this.i18n.t('security.title', 'Account security'), module: 'Account' },
      '/reports': { title: 'Reports', module: 'Reports' },

      // Inventory
      '/inventory/stock': { title: 'Stock Balance', module: 'Inventory' },
      '/inventory/movements': { title: 'Stock Movements', module: 'Inventory' },
      '/inventory/warehouses': { title: 'Warehouses', module: 'Inventory' },

      // HR
      '/hr/employees': { title: 'Employees', module: 'HR' },
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
      '/projects/billing': { title: 'Project Billing', module: 'Projects' },

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

    if (ROUTE_MAP[path]) {
      return ROUTE_MAP[path];
    }

    // Dynamic ID paths
    if (path.startsWith('/finance/invoices/')) return { title: 'Invoice Details', module: 'Finance' };
    if (path.startsWith('/finance/journals/')) return { title: 'Journal Details', module: 'Finance' };
    if (path.startsWith('/finance/receipts/')) return { title: 'Receipt Details', module: 'Finance' };
    if (path.startsWith('/finance/bills/')) return { title: 'Bill Details', module: 'Finance' };
    if (path.startsWith('/finance/payments/')) return { title: 'Payment Details', module: 'Finance' };
    if (path.startsWith('/parties/')) return { title: 'Party Details', module: 'Core' };
    if (path.startsWith('/reports/')) return { title: 'Financial Report', module: 'Reports' };
    if (path.startsWith('/sales/quotes/')) return { title: 'Quote Details', module: 'Sales' };
    if (path.startsWith('/sales/orders/')) return { title: 'Order Details', module: 'Sales' };
    if (path.startsWith('/purchasing/orders/')) return { title: 'Purchase Order Details', module: 'Purchasing' };
    if (path.startsWith('/projects/')) return { title: 'Project Details', module: 'Projects' };
    if (path.startsWith('/real-estate/properties/')) return { title: 'Property Details', module: 'Real Estate' };
    if (path.startsWith('/real-estate/leases/')) return { title: 'Lease Details', module: 'Real Estate' };
    if (path.startsWith('/hr/payroll/')) return { title: 'Payroll Run Details', module: 'HR' };

    const segs = path.split('/').filter(Boolean);
    if (segs.length > 0) {
      const last = segs[segs.length - 1];
      const humanized = last.charAt(0).toUpperCase() + last.slice(1).replace(/-/g, ' ');
      return { title: humanized };
    }

    return null;
  }
}
