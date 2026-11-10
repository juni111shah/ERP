import { DashboardChart, KpiCard } from '@core/api/admin.api';

/** The overview's business domains: which domain a KPI or chart belongs to, the KPI clusters, hero cards and chart groups. */

export type DomainCategory =
  | 'all'
  | 'finance'
  | 'sales-crm'
  | 'supply-operations'
  | 'projects-services'
  | 'real-estate'
  | 'hr';

/** A titled group of KPI cards of one or more domains. */
export interface KpiCluster {
  id: string;
  title: string;
  icon: string;
  category: DomainCategory;
  kpis: KpiCard[];
}

/** The tab title of each domain. */
const CATEGORY_TITLES: Partial<Record<DomainCategory, string>> = {
  finance: 'Finance & Treasury',
  'sales-crm': 'Sales & Commercial',
  'supply-operations': 'Supply Chain & Operations',
  'projects-services': 'Projects & Services',
  'real-estate': 'Real Estate Portfolio',
  hr: 'HR & Workforce',
};

export const categoryTitle = (cat: DomainCategory): string => CATEGORY_TITLES[cat] ?? 'Overview';

/** Chart groups of the overview: finance, sales, and everything operational. */
export const FINANCE_CHARTS: DomainCategory[] = ['finance'];
export const SALES_CHARTS: DomainCategory[] = ['sales-crm'];
export const OPS_CHARTS: DomainCategory[] = ['supply-operations', 'projects-services', 'real-estate', 'hr'];

export function classifyKpi(key: string): DomainCategory {
  const k = key.toLowerCase();
  // Real Estate
  if (['portfolio', 'occupancy', 'contracted-rent', 'expiring', 'work-orders'].includes(k) || k.startsWith('re-')) {
    return 'real-estate';
  }
  // HR
  if (['employees', 'payroll-runs', 'payroll-open', 'reviews', 'reviews-pending'].includes(k) || k.startsWith('hr-')) {
    return 'hr';
  }
  // Sales & CRM
  if (k.includes('sales') || k.includes('quote') || k.includes('lead') || k.includes('deal') || k.startsWith('crm-')) {
    return 'sales-crm';
  }
  // Projects & Services
  if (k.includes('project') || k.includes('task') || k.includes('timesheet') || k.includes('unbilled') || k.includes('engagement') || k.includes('milestone') || k.startsWith('svc-') || k.startsWith('prj-')) {
    return 'projects-services';
  }
  // Supply, Operations, POS, Retail, Distribution, Manufacturing, Inventory, Procurement
  if (k.includes('stock') || k.includes('inventory') || k.includes('purchase') || k.includes('requisition') || k.includes('procurement') || k.includes('pos') || k.includes('retail') || k.includes('route') || k.includes('trip') || k.includes('bom') || k.includes('work-order') || k.startsWith('mfg-') || k.startsWith('dist-')) {
    return 'supply-operations';
  }
  // Finance
  return 'finance';
}

export function classifyChart(key: string): DomainCategory {
  const k = key.toLowerCase();
  if (['occupancy', 'lease-expiries'].includes(k) || k.startsWith('re-')) return 'real-estate';
  if (k.includes('sales') || k.includes('crm') || k.includes('funnel') || k.includes('quote')) return 'sales-crm';
  if (k.includes('purchas') || k.includes('procure') || k.includes('mfg') || k.includes('production') || k.includes('stock')) return 'supply-operations';
  if (k.includes('project') || k.includes('task') || k.includes('service')) return 'projects-services';
  if (k.includes('hr') || k.includes('headcount') || k.includes('employee') || k.includes('people')) return 'hr';
  return 'finance';
}

/** The KPIs of a domain; `all` gives every KPI. */
export function categoryKpis(kpis: KpiCard[], cat: DomainCategory): KpiCard[] {
  if (cat === 'all') return kpis;
  return kpis.filter((k) => classifyKpi(k.key) === cat);
}

/** The charts of a domain; `all`, or a domain without charts of its own, gives every chart. */
export function categoryCharts(charts: DashboardChart[], cat: DomainCategory): DashboardChart[] {
  if (cat === 'all') return charts;
  const filtered = charts.filter((c) => classifyChart(c.key) === cat);
  return filtered.length > 0 ? filtered : charts;
}

/** The charts of a chart group. */
export const chartsIn = (charts: DashboardChart[], group: DomainCategory[]): DashboardChart[] =>
  charts.filter((c) => group.includes(classifyChart(c.key)));

/** The chart with `key`, else the first one; null when there are none. */
export function pickChart(charts: DashboardChart[], key: string | null): DashboardChart | null {
  if (charts.length === 0) return null;
  return (key ? charts.find((c) => c.key === key) : undefined) ?? charts[0];
}

/** The clusters' ids, icons and domains, in the order they are shown; the titles differ between the two views. */
const CLUSTERS: { id: string; icon: string; category: DomainCategory; members: DomainCategory[] }[] = [
  { id: 'treasury', icon: 'wallet', category: 'finance', members: ['finance'] },
  { id: 'commercial', icon: 'shopping-cart', category: 'sales-crm', members: ['sales-crm'] },
  { id: 'operations', icon: 'shop', category: 'supply-operations', members: ['supply-operations'] },
  { id: 'services', icon: 'solution', category: 'projects-services', members: ['projects-services', 'hr'] },
  { id: 'realestate', icon: 'home', category: 'real-estate', members: ['real-estate'] },
];

/** Titles of the clusters in the all-metrics matrix. */
export const MATRIX_CLUSTER_TITLES: Record<string, string> = {
  treasury: 'Finance, Treasury & Liquidity',
  commercial: 'Commercial, Retail & Sales Pipeline',
  operations: 'Supply Chain, Procurement & Production',
  services: 'Workforce, Projects & Client Services',
  realestate: 'Real Estate & Facilities Management',
};

/** Titles of the departmental clusters of the curated overview. */
export const OVERVIEW_CLUSTER_TITLES: Record<string, string> = {
  treasury: 'Treasury & Liquidity',
  commercial: 'Commercial & Sales Pipeline',
  operations: 'Supply Chain, Retail & Production',
  services: 'Workforce, Projects & Client Services',
  realestate: 'Real Estate & Facilities',
};

/** The KPIs grouped into the clusters, titled from `titles`. */
export function kpiClusters(kpis: KpiCard[], titles: Record<string, string>): KpiCluster[] {
  return CLUSTERS.map(({ id, icon, category, members }) => ({
    id,
    title: titles[id],
    icon,
    category,
    kpis: kpis.filter((k) => members.includes(classifyKpi(k.key))),
  }));
}

/** The 4 hero highlights of the overview: the priority measures first, topped up in the API's order. */
export function heroKpis(all: KpiCard[]): KpiCard[] {
  if (all.length <= 4) return all;

  const priorityKeys = ['portfolio', 'occupancy', 'receivables', 'net-result', 'cash', 'collected'];
  const chosen: KpiCard[] = [];

  for (const pk of priorityKeys) {
    const match = all.find((k) => k.key === pk);
    if (match && !chosen.includes(match)) {
      chosen.push(match);
    }
    if (chosen.length === 4) break;
  }

  for (const k of all) {
    if (chosen.length >= 4) break;
    if (!chosen.includes(k)) chosen.push(k);
  }
  return chosen;
}

/** The KPIs that are not among `heroes`. */
export const withoutHeroes = (kpis: KpiCard[], heroes: KpiCard[]): KpiCard[] => kpis.filter((k) => !heroes.some((h) => h.key === k.key));

/** KPIs whose label, key or caption contains the search text. */
export function searchKpis(kpis: KpiCard[], query: string): KpiCard[] {
  const q = query.trim().toLowerCase();
  if (!q) return kpis;
  return kpis.filter((k) =>
    k.label.toLowerCase().includes(q) ||
    k.key.toLowerCase().includes(q) ||
    (k.caption && k.caption.toLowerCase().includes(q))
  );
}
