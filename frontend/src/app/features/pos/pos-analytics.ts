import { DashboardChart } from '@core/api/admin.api';
import { PosSalesReportDto } from '@core/api/pos.api';
import { toIsoDate, toUtcIsoDate } from '@core/format/dates';

/** Periods and charts of the POS analytics page. */

/** The order statuses counted on the analytics page. */
export const ORDER_STATUSES = ['completed', 'parked', 'voided'];

/** The quick ranges of the POS analytics page. */
export type RangeKey = 'today' | 'yesterday' | '7d' | '30d' | 'month';

/** A reporting period as the API takes it; both ends left out mean the shop's own today. */
export interface ReportPeriod {
  from?: string;
  to?: string;
}

/** The quick-range buttons, in the order they are shown. */
export const RANGES: { key: RangeKey; label: string }[] = [
  { key: 'today', label: 'Today' },
  { key: 'yesterday', label: 'Yesterday' },
  { key: '7d', label: '7 days' },
  { key: '30d', label: '30 days' },
  { key: 'month', label: 'This month' },
];

/** Days counted on the shop's calendar date, whatever the browser's own clock says. */
const dayOffset = (isoDate: string, days: number): string => {
  const d = new Date(`${isoDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return toUtcIsoDate(d);
};

/**
 * The period to report: two picked dates win; otherwise the quick range counted back from the shop's today. Before the
 * first answer the shop's today is unknown, so no dates are sent and the server reports its own today.
 */
export function reportPeriod(range: RangeKey | null, picked: Date[] | null, shopToday: string | null): ReportPeriod {
  if (picked?.length === 2) return { from: toIsoDate(picked[0]), to: toIsoDate(picked[1]) };
  if (!shopToday) return {};
  switch (range) {
    case 'yesterday': return { from: dayOffset(shopToday, -1), to: dayOffset(shopToday, -1) };
    case '7d': return { from: dayOffset(shopToday, -6), to: shopToday };
    case '30d': return { from: dayOffset(shopToday, -29), to: shopToday };
    case 'month': return { from: `${shopToday.slice(0, 8)}01`, to: shopToday };
    default: return { from: shopToday, to: shopToday };
  }
}

/** Sales and returns per day of the period. */
export function dayChart(report: PosSalesReportDto | null): DashboardChart {
  const days = report?.days ?? [];
  return {
    key: 'pos-days', title: 'Sales by day', kind: 'bar', order: 1,
    categories: days.map((d) => d.date),
    series: [{ name: 'Sales', values: days.map((d) => d.sales) }, { name: 'Returns', values: days.map((d) => d.returns) }],
  } as DashboardChart;
}

/** Sales per hour of the day, to show the busiest hours. */
export function hourChart(report: PosSalesReportDto | null): DashboardChart {
  const hours = report?.hours ?? [];
  return {
    key: 'pos-hours', title: 'Busiest hours', kind: 'bar', order: 2, singleColor: true,
    categories: hours.map((h) => `${String(h.hour).padStart(2, '0')}:00`),
    series: [{ name: 'Sales', values: hours.map((h) => h.sales) }],
  } as DashboardChart;
}
