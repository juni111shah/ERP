import { DashboardChart } from '@core/api/admin.api';
import { OwnerSection, OwnerTable } from '@core/api/setup.api';
import { displayMoney } from '@core/format/display';
import { StatFormat } from '@shared/components/stat-card/stat-card.component';

/** The owner dashboard's sections: card formats, the section chart and table cells. */

/** A card's format as the stat card shows it; anything unknown is a plain number. */
export const ownerStatFormat = (f: string): StatFormat => (f === 'money' || f === 'percent' || f === 'number' ? f : 'number');

/** The section's chart has at least one value other than zero. */
const hasChartValues = (s: OwnerSection): boolean => !!s.chart && s.chart.series.some((x) => x.values.some((v) => v !== 0));

/** A section with no figures, no table rows and no chart values: the page says so instead of showing nothing. */
export function isEmptySection(s: OwnerSection): boolean {
  return s.cards.length === 0 && !(s.table && s.table.rows.length > 0) && !hasChartValues(s);
}

/** The section's chart as a bar chart, or null when it has no values. */
export function sectionChart(s: OwnerSection): DashboardChart | null {
  if (!s.chart || !hasChartValues(s)) return null;
  return { key: s.key, title: s.title, kind: 'bar', categories: s.chart.categories, series: s.chart.series, order: 0 };
}

const columnFormat = (t: OwnerTable, column: number): string => t.columnFormats?.[column] ?? 'text';

/** The column holds figures (right-aligned). */
export const isNumericColumn = (t: OwnerTable, column: number): boolean => columnFormat(t, column) !== 'text';

/** A table cell in its column's format: money, a percentage or a number; text and blanks as they are. */
export function ownerCellText(t: OwnerTable, column: number, cell: string): string {
  const format = columnFormat(t, column);
  const n = Number(cell);
  if (format === 'text' || cell === '' || Number.isNaN(n)) return cell;
  if (format === 'money') return displayMoney(n, null);
  if (format === 'percent') return `${n.toLocaleString('en-US', { maximumFractionDigits: 1 })}%`;
  return n.toLocaleString('en-US');
}
