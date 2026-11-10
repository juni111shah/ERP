import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { DashboardChart } from '@core/api/admin.api';
import { PosKpiApi, PosOrdersApi, PosRegisterDto, PosRegistersApi, PosSalesGroupDto, PosSalesReportDto, PosSessionSummaryDto, PosSessionsApi } from '@core/api/pos.api';
import { toIsoDate } from '@core/format/dates';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { SimpleChartComponent } from '@shared/components/simple-chart/simple-chart.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { countByStatus, StatusCount, StatusCountsComponent } from '@features/sales/components/status-counts/status-counts.component';
import { TENDER_LABELS } from '../../pos-cart';

const ORDER_STATUSES = ['completed', 'parked', 'voided'];

type RangeKey = 'today' | 'yesterday' | '7d' | '30d' | 'month';

/** Days counted on the shop's calendar date, whatever the browser's own clock says. */
const dayOffset = (isoDate: string, days: number): string => {
  const d = new Date(`${isoDate}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

/** End-of-day and period figures: what was sold, how it was paid, who sold it and what sold best. */
@Component({
  selector: 'erp-pos-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NgTemplateOutlet, FormsModule, RouterModule, NzButtonModule, NzDatePickerModule, NzGridModule, NzIconModule, NzSelectModule, NzSkeletonModule, ErpDatePipe, SimpleChartComponent, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  templateUrl: './pos-analytics.page.html',
  styleUrl: './pos-analytics.page.scss',
})
export class PosAnalyticsPage {
  private readonly kpiApi = inject(PosKpiApi);
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly registersApi = inject(PosRegistersApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly ranges: { key: RangeKey; label: string }[] = [
    { key: 'today', label: 'Today' },
    { key: 'yesterday', label: 'Yesterday' },
    { key: '7d', label: '7 days' },
    { key: '30d', label: '30 days' },
    { key: 'month', label: 'This month' },
  ];
  private readonly shopToday = signal<string | null>(null);
  readonly range = signal<RangeKey | null>('today');
  readonly picked = signal<Date[] | null>(null);
  readonly registerId = signal<string | null>(null);
  readonly registers = signal<PosRegisterDto[]>([]);

  readonly report = signal<PosSalesReportDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly orders = signal<StatusCount[] | null>(null);
  readonly countsFailed = signal(false);
  readonly shifts = signal<PosSessionSummaryDto[] | null>(null);
  readonly shiftsFailed = signal(false);

  readonly dayChart = computed<DashboardChart>(() => {
    const days = this.report()?.days ?? [];
    return {
      key: 'pos-days', title: 'Sales by day', kind: 'bar', order: 1,
      categories: days.map((d) => d.date),
      series: [{ name: 'Sales', values: days.map((d) => d.sales) }, { name: 'Returns', values: days.map((d) => d.returns) }],
    } as DashboardChart;
  });

  readonly hourChart = computed<DashboardChart>(() => {
    const hours = this.report()?.hours ?? [];
    return {
      key: 'pos-hours', title: 'Busiest hours', kind: 'bar', order: 2, singleColor: true,
      categories: hours.map((h) => `${String(h.hour).padStart(2, '0')}:00`),
      series: [{ name: 'Sales', values: hours.map((h) => h.sales) }],
    } as DashboardChart;
  });

  constructor() {
    this.registersApi.list({ page: 1, pageSize: 100 }).subscribe({ next: (r) => this.registers.set(r.items), error: () => this.registers.set([]) });
    this.loadMetrics();
  }

  tender(method: string): string {
    return TENDER_LABELS[method as keyof typeof TENDER_LABELS] ?? method;
  }

  asGroups(rows: unknown): PosSalesGroupDto[] {
    return rows as PosSalesGroupDto[];
  }

  setRange(key: RangeKey): void {
    this.range.set(key);
    this.picked.set(null);
    this.loadReport();
  }

  onPicked(dates: Date[] | null): void {
    this.picked.set(dates);
    if (dates?.length === 2) {
      this.range.set(null);
      this.loadReport();
    }
  }

  setRegister(id: string | null): void {
    this.registerId.set(id);
    this.loadReport();
  }

  /** No dates for today: the server knows the shop's day, and the first answer tells this page what day that is. */
  private period(): { from?: string; to?: string } {
    const picked = this.picked();
    if (picked?.length === 2) return { from: toIsoDate(picked[0]), to: toIsoDate(picked[1]) };
    const today = this.shopToday();
    if (!today) return {};
    switch (this.range()) {
      case 'yesterday': return { from: dayOffset(today, -1), to: dayOffset(today, -1) };
      case '7d': return { from: dayOffset(today, -6), to: today };
      case '30d': return { from: dayOffset(today, -29), to: today };
      case 'month': return { from: `${today.slice(0, 8)}01`, to: today };
      default: return { from: today, to: today };
    }
  }

  loadMetrics(): void {
    this.loadReport();
    this.loadCounts();
    this.loadShifts();
  }

  private loadReport(): void {
    const { from, to } = this.period();
    this.loading.set(true);
    this.failed.set(false);
    this.kpiApi.salesReport(from, to, this.registerId()).subscribe({
      next: (r) => {
        if (!this.shopToday()) this.shopToday.set(r.to);
        this.report.set(r);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  loadCounts(): void {
    this.countsFailed.set(false);
    countByStatus((q) => this.ordersApi.list(q), ORDER_STATUSES).subscribe((rows) => {
      this.orders.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
  }

  loadShifts(): void {
    this.shiftsFailed.set(false);
    this.sessionsApi.list({ page: 1, pageSize: 20, filters: [{ field: 'status', op: 'eq', value: 'open' }] }).subscribe({
      next: (r) => this.shifts.set(r.items),
      error: () => this.shiftsFailed.set(true),
    });
  }
}
