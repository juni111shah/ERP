import { DashboardChart } from '@core/api/admin.api';
import { PlatformDashboard, SubscriptionStatus } from '@core/api/platform.api';
import { statusWords } from './platform-ui';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** One row of the "clients by status" card. */
export interface StatusCount { key: string; tone: string; label: string; count: number; }

/** The "collected" bar chart: one bar per month ("2026-11" shows as "Nov 2026"). */
export function collectedChart(d: PlatformDashboard): DashboardChart {
  return {
    key: 'platform-collected', title: 'Collected', kind: 'bar', order: 0, singleColor: true,
    categories: d.collected.map((m) => { const [y, mm] = m.month.split('-'); return `${MONTHS[+mm - 1]} ${y}`; }),
    series: [{ name: 'Collected', values: d.collected.map((m) => m.amount) }],
  };
}

/** How many clients are in each status, the paying ones first, and those without a plan last. */
export function statusCounts(d: PlatformDashboard): StatusCount[] {
  const row = (key: SubscriptionStatus, count: number): StatusCount => ({ key, tone: key, label: statusWords(key), count });
  return [row('active', d.active), row('trialing', d.trialing), row('pastDue', d.pastDue), row('expired', d.expired), row('suspended', d.suspended), row('cancelled', d.cancelled), { key: 'none', tone: 'inactive', label: 'No plan', count: d.withoutPlan }];
}
