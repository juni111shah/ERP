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
import { PosKpiApi, PosOrdersApi, PosRegisterDto, PosRegistersApi, PosSalesGroupDto, PosSalesReportDto, PosSessionSummaryDto, PosSessionsApi } from '@core/api/pos.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SimpleChartComponent } from '@shared/components/simple-chart/simple-chart.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';
import { countByStatus, StatusCount, StatusCountsComponent } from '@shared/components/status-counts/status-counts.component';
import { dayChart, hourChart, ORDER_STATUSES, RANGES, RangeKey, reportPeriod } from '../../pos-analytics';
import { tenderName } from '../../pos-cart';

/** End-of-day and period figures: what was sold, how it was paid, who sold it and what sold best. */
@Component({
  selector: 'erp-pos-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NgTemplateOutlet, FormsModule, RouterModule, NzButtonModule, NzDatePickerModule, NzGridModule, NzIconModule, NzSelectModule, NzSkeletonModule, ErpDatePipe, HasPermissionDirective, CanPipe, SimpleChartComponent, StatCardComponent, StatePanelComponent, StatusCountsComponent],
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
  readonly ranges = RANGES;
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

  readonly dayChart = computed(() => dayChart(this.report()));
  readonly hourChart = computed(() => hourChart(this.report()));

  constructor() {
    this.registersApi.list({ page: 1, pageSize: 100 }).subscribe({ next: (r) => this.registers.set(r.items), error: () => this.registers.set([]) });
    this.loadMetrics();
  }

  tender(method: string): string {
    return tenderName(method);
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

  loadMetrics(): void {
    this.loadReport();
    this.loadCounts();
    this.loadShifts();
  }

  private loadReport(): void {
    // No dates for today: the server knows the shop's day, and the first answer tells this page what day that is.
    const { from, to } = reportPeriod(this.range(), this.picked(), this.shopToday());
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
