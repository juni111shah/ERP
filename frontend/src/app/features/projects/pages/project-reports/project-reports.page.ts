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
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { CsvExportService } from '@core/services/csv-export.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { PRJ_STYLES, ProjectTagComponent } from '../../projects-ui';

type Tab = 'profit' | 'budget' | 'util' | 'ageing' | 'hours';
const sum = <T>(rows: T[], f: (r: T) => number): number => rows.reduce((a, r) => a + (f(r) || 0), 0);

/** Project reports with a chart, totals, a link to each project and an export: profit, budget and earned value, utilisation, unbilled ageing, hours. */
@Component({
  selector: 'erp-project-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzDatePickerModule, NzIconModule, NzSelectModule, NzTableModule, ApexChartComponent, ProjectTagComponent],
  template: `
    <div class="prj-toolbar">
      <nz-range-picker [(ngModel)]="range" (ngModelChange)="loadAll()" aria-label="Period" data-testid="report-range"></nz-range-picker>
      <div class="prj-chips" role="group" aria-label="Quick periods">
        @for (p of presets; track p.key) { <button type="button" class="prj-chip" (click)="preset(p.key)">{{ p.label }}</button> }
      </div>
      <span class="prj-spacer"></span>
      <button nz-button (click)="export()" data-testid="report-export"><span nz-icon nzType="download"></span> Export CSV</button>
    </div>

    <nav class="tabs" role="tablist" aria-label="Report">
      @for (t of tabs; track t.key) { <button type="button" role="tab" [class.on]="tab() === t.key" [attr.aria-selected]="tab() === t.key" (click)="tab.set(t.key)" [attr.data-testid]="'report-tab-' + t.key">{{ t.label }}</button> }
    </nav>

    @if (chart(); as c) { <div class="prj-card chart"><erp-apex-chart [chart]="c" /></div> }

    <div class="prj-card prj-table-wrap">
      @switch (tab()) {
        @case ('profit') {
          <nz-table [nzData]="profit()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-profit">
            <thead><tr><th>Project</th><th>Customer</th><th class="prj-num">Hours</th><th class="prj-num">Billed</th><th class="prj-num">Unbilled</th><th class="prj-num">Labour cost</th><th class="prj-num">Expenses</th><th class="prj-num">Margin</th><th class="prj-num">Margin %</th></tr></thead>
            <tbody>
              @for (r of profit(); track r.projectId) {
                <tr><td><a class="prj-link" [routerLink]="['/projects', r.projectId]">{{ r.number }} · {{ r.name }}</a></td><td>{{ r.customerName }}</td><td class="prj-num">{{ r.hours | number: '1.1-1' }}</td><td class="prj-num">{{ r.billed | number: '1.0-0' }}</td><td class="prj-num">{{ r.unbilled | number: '1.0-0' }}</td><td class="prj-num">{{ r.labourCost | number: '1.0-0' }}</td><td class="prj-num">{{ r.expenseCost | number: '1.0-0' }}</td>
                  <td class="prj-num" [class.prj-danger]="r.margin < 0">{{ r.margin | number: '1.0-0' }}</td><td class="prj-num" [class.prj-danger]="r.margin < 0">{{ r.marginPercent | number: '1.0-1' }}%</td></tr>
              } @empty { <tr><td colspan="9" class="prj-empty">No projects.</td></tr> }
              @if (profit().length > 1) {
                <tr class="total" data-testid="rep-profit-total"><td colspan="2">Total</td><td class="prj-num">{{ totals().hours | number: '1.1-1' }}</td><td class="prj-num">{{ totals().billed | number: '1.0-0' }}</td><td class="prj-num">{{ totals().unbilled | number: '1.0-0' }}</td><td class="prj-num">{{ totals().labour | number: '1.0-0' }}</td><td class="prj-num">{{ totals().expense | number: '1.0-0' }}</td><td class="prj-num">{{ totals().margin | number: '1.0-0' }}</td><td class="prj-num">{{ totals().marginPercent | number: '1.0-1' }}%</td></tr>
              }
            </tbody>
          </nz-table>
        }
        @case ('budget') {
          <nz-table [nzData]="budget()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-budget">
            <thead><tr><th>Project</th><th>Health</th><th class="prj-num">Budget</th><th class="prj-num">Cost so far</th><th class="prj-num">Used</th><th class="prj-num">Done</th><th class="prj-num">Forecast cost</th><th class="prj-num" title="Earned value divided by the cost so far. Below 1 means spending more than the work done is worth.">Cost index</th><th class="prj-num" title="Earned value divided by the planned value. Below 1 means behind plan.">Schedule index</th><th class="prj-num">Earned</th><th class="prj-num">Billed</th><th class="prj-num">WIP</th></tr></thead>
            <tbody>
              @for (r of budget(); track r.projectId) {
                <tr><td><a class="prj-link" [routerLink]="['/projects', r.projectId]">{{ r.number }} · {{ r.name }}</a></td><td><erp-prj-tag kind="health" [value]="r.health" /></td><td class="prj-num">{{ r.budget | number: '1.0-0' }}</td><td class="prj-num">{{ r.cost | number: '1.0-0' }}</td><td class="prj-num" [class.prj-danger]="r.usedPercent > 100">{{ r.usedPercent | number: '1.0-0' }}%</td>
                  <td class="prj-num">{{ r.percentComplete | number: '1.0-0' }}%</td><td class="prj-num" [class.prj-danger]="r.budget > 0 && r.forecastVariance < 0">{{ r.forecastCost | number: '1.0-0' }}</td>
                  <td class="prj-num" [class.prj-danger]="(r.costIndex ?? 1) < 1">{{ r.costIndex ?? '' }}</td><td class="prj-num" [class.prj-danger]="(r.scheduleIndex ?? 1) < 1">{{ r.scheduleIndex ?? '' }}</td>
                  <td class="prj-num">{{ r.billingType === 'fixedFee' ? (r.earnedRevenue | number: '1.0-0') : '' }}</td><td class="prj-num">{{ r.billed | number: '1.0-0' }}</td><td class="prj-num">{{ r.billingType === 'fixedFee' ? (r.workInProgress | number: '1.0-0') : '' }}</td></tr>
              } @empty { <tr><td colspan="12" class="prj-empty">No projects.</td></tr> }
            </tbody>
          </nz-table>
          <p class="prj-muted hint">WIP is the revenue earned by the share of work done (fixed-fee projects) minus what has been billed. A cost or schedule index below 1 means over budget or behind plan.</p>
        }
        @case ('util') {
          <nz-table [nzData]="util()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-util">
            <thead><tr><th>Person</th><th class="prj-num">Logged</th><th class="prj-num">Billable</th><th class="prj-num">Capacity</th><th class="prj-num">Planned</th><th class="prj-num">Utilisation</th><th class="prj-num">Billable share</th></tr></thead>
            <tbody>
              @for (r of util(); track r.person) {
                <tr><td>{{ r.person }}</td><td class="prj-num">{{ r.loggedHours | number: '1.1-1' }} h</td><td class="prj-num">{{ r.billableHours | number: '1.1-1' }} h</td><td class="prj-num">{{ r.capacityHours | number: '1.0-0' }} h</td><td class="prj-num" [class.prj-danger]="r.allocatedHours > r.capacityHours">{{ r.allocatedHours | number: '1.0-0' }} h</td><td class="prj-num" [class.prj-danger]="r.utilisationPercent > 100">{{ r.utilisationPercent | number: '1.0-0' }}%</td><td class="prj-num">{{ r.billablePercent | number: '1.0-0' }}%</td></tr>
              } @empty { <tr><td colspan="7" class="prj-empty">No time logged in this period.</td></tr> }
            </tbody>
          </nz-table>
        }
        @case ('ageing') {
          <nz-table [nzData]="ageing()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-ageing">
            <thead><tr><th>Project</th><th class="prj-num">0-30 days</th><th class="prj-num">31-60</th><th class="prj-num">61-90</th><th class="prj-num">Over 90</th><th class="prj-num">Total</th></tr></thead>
            <tbody>
              @for (r of ageing(); track r.projectId) {
                <tr><td><a class="prj-link" [routerLink]="['/projects', r.projectId]" [queryParams]="{ tab: 'money' }">{{ r.number }} · {{ r.name }}</a></td><td class="prj-num">{{ r.current | number: '1.0-0' }}</td><td class="prj-num">{{ r.days31To60 | number: '1.0-0' }}</td><td class="prj-num">{{ r.days61To90 | number: '1.0-0' }}</td><td class="prj-num" [class.prj-danger]="r.over90 > 0">{{ r.over90 | number: '1.0-0' }}</td><td class="prj-num"><b>{{ r.total | number: '1.0-0' }}</b></td></tr>
              } @empty { <tr><td colspan="6" class="prj-empty">Nothing is waiting to be billed.</td></tr> }
            </tbody>
          </nz-table>
        }
        @case ('hours') {
          <div class="prj-toolbar">
            <nz-select [(ngModel)]="groupBy" (ngModelChange)="loadHours()" style="width:180px" aria-label="Group hours by" data-testid="hours-group">
              <nz-option nzValue="person" nzLabel="By person"></nz-option><nz-option nzValue="project" nzLabel="By project"></nz-option><nz-option nzValue="task" nzLabel="By task"></nz-option>
            </nz-select>
          </div>
          <nz-table [nzData]="hours()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-hours">
            <thead><tr><th>Group</th><th class="prj-num">Hours</th><th class="prj-num">Billable</th><th class="prj-num">Value</th><th class="prj-num">Cost</th></tr></thead>
            <tbody>
              @for (r of hours(); track r.group) {
                <tr><td>{{ r.group }}</td><td class="prj-num">{{ r.hours | number: '1.1-1' }}</td><td class="prj-num">{{ r.billableHours | number: '1.1-1' }}</td><td class="prj-num">{{ r.amount | number: '1.0-0' }}</td><td class="prj-num">{{ r.cost | number: '1.0-0' }}</td></tr>
              } @empty { <tr><td colspan="5" class="prj-empty">No time logged in this period.</td></tr> }
            </tbody>
          </nz-table>
        }
      }
    </div>
    <p class="prj-muted hint">Amounts are in {{ currency() }}.</p>
  `,
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
