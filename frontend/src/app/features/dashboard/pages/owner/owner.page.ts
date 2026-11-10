import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DashboardChart } from '@core/api/admin.api';
import { OwnerDashboardApi, OwnerDashboardDto, OwnerSection, OwnerTable } from '@core/api/setup.api';
import { displayDate, displayMoney } from '@core/format/display';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { RangeControlComponent } from '@shared/components/range-control/range-control.component';
import { StatCardComponent, StatFormat } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { DashboardRange, presetRange, rangeFromParams, storedRange } from '../dashboard/dashboard.page';
import { ActivatedRoute } from '@angular/router';

/** The owner's view of the business: profit, cash, what is owed, what is due, branches, budget and the business's own measures. */
@Component({
  selector: 'erp-owner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzCardModule, NzSelectModule, NzSkeletonModule, NzTableModule, ApexChartComponent, RangeControlComponent, StatCardComponent, StatePanelComponent],
  styleUrl: './owner.page.scss',
  templateUrl: './owner.page.html',
})
export class OwnerPage {
  private readonly api = inject(OwnerDashboardApi);
  private readonly session = inject(SessionStore);
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService);
  private readonly route = inject(ActivatedRoute);

  readonly range = signal<DashboardRange>(rangeFromParams(this.route.snapshot.queryParamMap) ?? storedRange() ?? presetRange('1y'));
  readonly entityId = signal<string | null>(this.session.currentEntityId());
  readonly data = signal<OwnerDashboardDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly entities = computed(() => this.session.me()?.legalEntities ?? []);
  readonly allEntities = computed(() => this.session.me()?.allLegalEntities ?? false);
  /** The currency of the figures: the one the server found for the chosen entity, else the workspace's. */
  readonly currencyCode = computed(() => this.data()?.currency || this.workspaceCurrency.currency());

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    const { from, to } = this.range();
    this.api.get({ from, to, legalEntityId: this.entityId() }).subscribe({
      next: (d) => { this.loading.set(false); this.data.set(d); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  setRange(r: DashboardRange): void { this.range.set(r); this.load(); }
  setEntity(id: string | null): void { this.entityId.set(id); this.load(); }

  date(v: string): string { return displayDate(v); }
  format(f: string): StatFormat { return f === 'money' || f === 'percent' || f === 'number' ? f : 'number'; }

  /** A section with no figures, no table rows and no chart values: the page says so instead of showing nothing. */
  isEmpty(s: OwnerSection): boolean {
    return s.cards.length === 0 && !(s.table && s.table.rows.length > 0) && !this.hasChartValues(s);
  }
  private hasChartValues(s: OwnerSection): boolean { return !!s.chart && s.chart.series.some((x) => x.values.some((v) => v !== 0)); }

  chartOf(s: OwnerSection): DashboardChart | null {
    if (!s.chart || !this.hasChartValues(s)) return null;
    return { key: s.key, title: s.title, kind: 'bar', categories: s.chart.categories, series: s.chart.series, order: 0 };
  }

  isNumeric(t: OwnerTable, column: number): boolean { return (t.columnFormats?.[column] ?? 'text') !== 'text'; }
  cellText(t: OwnerTable, column: number, cell: string): string {
    const format = t.columnFormats?.[column] ?? 'text';
    const n = Number(cell);
    if (format === 'text' || cell === '' || Number.isNaN(n)) return cell;
    if (format === 'money') return displayMoney(n, null);
    if (format === 'percent') return `${n.toLocaleString('en-US', { maximumFractionDigits: 1 })}%`;
    return n.toLocaleString('en-US');
  }
}
