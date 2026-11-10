import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DashboardChart } from '@core/api/admin.api';
import { AgeingRow, BudgetRow, HoursRow, ProfitabilityRow, ProjectsExtraApi, UtilisationRow } from '@core/api/projects-extra.api';
import { toIsoDate } from '@core/format/dates';
import { CsvExportService } from '@core/services/csv-export.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { PRJ_STYLES } from '../../projects-ui';

type Tab = 'profit' | 'budget' | 'util' | 'ageing' | 'hours';
const sum = <T>(rows: T[], f: (r: T) => number): number => rows.reduce((a, r) => a + (f(r) || 0), 0);

/** Project reports with a chart, totals, a link to each project and an export: profit, budget and earned value, utilisation, unbilled ageing, hours. */
@Component({
  selector: 'erp-project-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzDatePickerModule, NzIconModule, NzSelectModule, NzTableModule, ApexChartComponent, ProjectTagComponent],
  templateUrl: './project-reports.page.html',
  styles: [PRJ_STYLES + `
    .tabs { display:flex; gap:4px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); margin-bottom:12px; overflow-x:auto; }
    .tabs button { background:none; border:0; border-bottom:2px solid transparent; padding:8px 12px; cursor:pointer; color:var(--erp-color-text-secondary,#475569); white-space:nowrap; } .tabs button.on { color:var(--erp-color-primary,#10955d); border-bottom-color:var(--erp-color-primary,#10955d); font-weight:600; }
    .chart { margin-bottom:12px; } .total td { font-weight:700; background:var(--erp-color-bg-subtle,#f8fafc); } .hint { margin-top:8px; }
  `],
})
export class ProjectReportsPage {
  private readonly api = inject(ProjectsExtraApi);
  private readonly csv = inject(CsvExportService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly tabs: { key: Tab; label: string }[] = [{ key: 'profit', label: 'Profitability' }, { key: 'budget', label: 'Budget and earned value' }, { key: 'util', label: 'Utilisation' }, { key: 'ageing', label: 'Unbilled ageing' }, { key: 'hours', label: 'Hours' }];
  readonly presets = [{ key: 'month', label: 'This month' }, { key: 'last', label: 'Last month' }, { key: 'quarter', label: 'Last 3 months' }, { key: 'year', label: 'This year' }];
  readonly tab = signal<Tab>('profit');
  range: Date[] = [new Date(new Date().getFullYear(), new Date().getMonth(), 1), new Date()];
  groupBy = 'person';
  readonly profit = signal<ProfitabilityRow[]>([]);
  readonly util = signal<UtilisationRow[]>([]);
  readonly budget = signal<BudgetRow[]>([]);
  readonly ageing = signal<AgeingRow[]>([]);
  readonly hours = signal<HoursRow[]>([]);

  readonly totals = computed(() => {
    const p = this.profit();
    const billed = sum(p, (r) => r.billed); const unbilled = sum(p, (r) => r.unbilled); const margin = sum(p, (r) => r.margin);
    return { hours: sum(p, (r) => r.hours), billed, unbilled, labour: sum(p, (r) => r.labourCost), expense: sum(p, (r) => r.expenseCost), margin, marginPercent: billed + unbilled ? (margin / (billed + unbilled)) * 100 : 0 };
  });

  /** A chart of the top rows of the report on screen. */
  readonly chart = computed<DashboardChart | null>(() => {
    const make = (title: string, categories: string[], series: { name: string; values: number[] }[], kind: 'bar' | 'stacked' | 'hbar' = 'bar'): DashboardChart | null => (categories.length ? { key: 'projects-' + this.tab(), title, kind, categories, series, order: 0 } : null);
    const top = <T>(rows: T[], f: (r: T) => number): T[] => [...rows].sort((a, b) => f(b) - f(a)).slice(0, 8);
    switch (this.tab()) {
      case 'profit': { const r = top(this.profit(), (x) => x.billed + x.unbilled + x.cost); return make('Revenue and cost by project', r.map((x) => x.number), [{ name: 'Revenue', values: r.map((x) => x.billed + x.unbilled) }, { name: 'Cost', values: r.map((x) => x.cost) }]); }
      case 'budget': { const r = top(this.budget(), (x) => x.budget); return make('Budget and cost so far', r.map((x) => x.number), [{ name: 'Budget', values: r.map((x) => x.budget) }, { name: 'Cost so far', values: r.map((x) => x.cost) }, { name: 'Forecast', values: r.map((x) => x.forecastCost) }]); }
      case 'util': { const r = top(this.util(), (x) => x.capacityHours); return make('Hours by person', r.map((x) => x.person), [{ name: 'Logged', values: r.map((x) => x.loggedHours) }, { name: 'Planned', values: r.map((x) => x.allocatedHours) }, { name: 'Capacity', values: r.map((x) => x.capacityHours) }]); }
      case 'ageing': { const r = top(this.ageing(), (x) => x.total); return make('How old the unbilled work is', r.map((x) => x.number), [{ name: '0-30 days', values: r.map((x) => x.current) }, { name: '31-60', values: r.map((x) => x.days31To60) }, { name: '61-90', values: r.map((x) => x.days61To90) }, { name: 'Over 90', values: r.map((x) => x.over90) }], 'stacked'); }
      default: { const r = top(this.hours(), (x) => x.hours); return make('Hours', r.map((x) => x.group), [{ name: 'Billable', values: r.map((x) => x.billableHours) }, { name: 'Other', values: r.map((x) => x.hours - x.billableHours) }], 'stacked'); }
    }
  });

  constructor() { this.loadAll(); }

  preset(key: string): void {
    const now = new Date(); const y = now.getFullYear(); const m = now.getMonth();
    this.range = key === 'month' ? [new Date(y, m, 1), now] : key === 'last' ? [new Date(y, m - 1, 1), new Date(y, m, 0)] : key === 'quarter' ? [new Date(y, m - 3, now.getDate()), now] : [new Date(y, 0, 1), now];
    this.loadAll();
  }

  private bounds(): { from: string; to: string } {
    const [a, b] = this.range?.length === 2 ? this.range : [new Date(), new Date()];
    return { from: toIsoDate(a), to: toIsoDate(b) };
  }

  loadAll(): void {
    const { from, to } = this.bounds();
    this.api.profitability(from, to).subscribe((r) => this.profit.set(r));
    this.api.utilisation(from, to).subscribe((r) => this.util.set(r));
    this.api.budget().subscribe((r) => this.budget.set(r));
    this.api.ageing(to).subscribe((r) => this.ageing.set(r));
    this.loadHours();
  }

  loadHours(): void {
    const { from, to } = this.bounds();
    this.api.hours(from, to, this.groupBy).subscribe((r) => this.hours.set(r));
  }

  export(): void {
    const c = (field: string, header: string) => ({ field, header });
    const save = (rows: unknown[], name: string, cols: { field: string; header: string }[]) => this.csv.exportToCsv(rows as Record<string, unknown>[], name, cols);
    switch (this.tab()) {
      case 'profit': save(this.profit(), 'project-profitability', [c('number', 'Project'), c('name', 'Name'), c('customerName', 'Customer'), c('hours', 'Hours'), c('billed', 'Billed'), c('unbilled', 'Unbilled'), c('labourCost', 'Labour cost'), c('expenseCost', 'Expenses'), c('margin', 'Margin'), c('marginPercent', 'Margin %')]); break;
      case 'budget': save(this.budget(), 'project-budget', [c('number', 'Project'), c('name', 'Name'), c('health', 'Health'), c('budget', 'Budget'), c('cost', 'Cost so far'), c('usedPercent', 'Used %'), c('percentComplete', 'Done %'), c('forecastCost', 'Forecast'), c('plannedValue', 'Planned value'), c('earnedValue', 'Earned value'), c('costIndex', 'Cost index'), c('scheduleIndex', 'Schedule index'), c('billed', 'Billed')]); break;
      case 'util': save(this.util(), 'project-utilisation', [c('person', 'Person'), c('loggedHours', 'Logged'), c('billableHours', 'Billable'), c('capacityHours', 'Capacity'), c('allocatedHours', 'Planned'), c('utilisationPercent', 'Utilisation %'), c('billablePercent', 'Billable %')]); break;
      case 'ageing': save(this.ageing(), 'project-unbilled-ageing', [c('number', 'Project'), c('name', 'Name'), c('current', '0-30'), c('days31To60', '31-60'), c('days61To90', '61-90'), c('over90', 'Over 90'), c('total', 'Total')]); break;
      default: save(this.hours(), 'project-hours', [c('group', 'Group'), c('hours', 'Hours'), c('billableHours', 'Billable'), c('amount', 'Value'), c('cost', 'Cost')]);
    }
  }
}
