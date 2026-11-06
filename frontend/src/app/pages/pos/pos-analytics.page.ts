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
import { DashboardChart } from '../../core/api/admin.api';
import { PosKpiApi, PosOrdersApi, PosRegisterDto, PosRegistersApi, PosSalesGroupDto, PosSalesReportDto, PosSessionSummaryDto, PosSessionsApi } from '../../core/api/pos.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { ErpDatePipe } from '../../shared/display.pipes';
import { SimpleChartComponent } from '../../shared/simple-chart.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { countByStatus, StatusCount, StatusCountsComponent } from '../sales/status-counts.component';
import { TENDER_LABELS } from './pos-cart';

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
  template: `
    <h1 class="erp-page-title">Point of Sale Analytics</h1>

    <div class="toolbar">
      <div class="ranges" role="group" aria-label="Period">
        @for (r of ranges; track r.key) {
          <button nz-button type="button" [nzType]="range() === r.key ? 'primary' : 'default'" (click)="setRange(r.key)" [attr.data-testid]="'pos-range-' + r.key">{{ r.label }}</button>
        }
        <nz-range-picker [ngModel]="picked()" (ngModelChange)="onPicked($event)" aria-label="Custom period" data-testid="pos-range-custom"></nz-range-picker>
      </div>
      <nz-select class="register" [ngModel]="registerId()" (ngModelChange)="setRegister($event)" nzAllowClear nzPlaceHolder="All registers" aria-label="Register">
        @for (r of registers(); track r.id) { <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option> }
      </nz-select>
      <button nz-button type="button" (click)="loadMetrics()" [nzLoading]="loading()" data-testid="pos-analytics-refresh"><span nz-icon nzType="reload"></span>Refresh</button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The point of sale figures could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadMetrics()" />
    } @else {
      @let r = report();
      <div class="erp-stat-grid erp-stat-grid--auto six" data-testid="pos-analytics-stats">
        <erp-stat-card label="Net takings" [value]="r?.netTakings ?? 0" format="money" caption="sales less returns" [loading]="loading()" [tone]="0" />
        <erp-stat-card label="Sales" [value]="r?.salesTotal ?? 0" format="money" [caption]="(r?.saleCount ?? 0) + ' sales, tax included'" [loading]="loading()" [tone]="1" />
        <erp-stat-card label="Returns" [value]="r?.returns ?? 0" format="money" [caption]="(r?.returnCount ?? 0) + ' returns'" [loading]="loading()" [tone]="3" />
        <erp-stat-card label="Discounts" [value]="r?.discounts ?? 0" format="money" caption="given on completed sales" [loading]="loading()" [tone]="2" />
        <erp-stat-card label="Average ticket" [value]="r?.averageTicket ?? 0" format="money" caption="per completed sale" [loading]="loading()" [tone]="4" />
        <erp-stat-card label="Cash over/short" [value]="r?.cashOverShort ?? 0" format="money" caption="on shifts opened in this period" [loading]="loading()" [tone]="5" />
      </div>

      <div nz-row [nzGutter]="[16, 16]" class="grid-row">
        <div nz-col [nzXs]="24" [nzLg]="14">
          <section class="panel" data-testid="pos-days">
            <h3>Sales by day</h3>
            <erp-apex-chart [chart]="dayChart()" />
          </section>
        </div>
        <div nz-col [nzXs]="24" [nzLg]="10">
          <section class="panel" data-testid="pos-hours">
            <h3>Busiest hours</h3>
            <erp-apex-chart [chart]="hourChart()" />
          </section>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <section class="panel" data-testid="pos-tenders">
            <h3>By payment</h3>
            <table>
              <thead><tr><th>Method</th><th class="amt">Taken</th><th class="amt">Refunded</th><th class="amt">Net</th></tr></thead>
              <tbody>
                @for (t of r?.tenders ?? []; track t.method) {
                  <tr><td>{{ tender(t.method) }}</td><td class="amt">{{ t.sales | number:'1.2-2' }}</td><td class="amt">{{ t.refunds | number:'1.2-2' }}</td><td class="amt strong">{{ t.net | number:'1.2-2' }}</td></tr>
                } @empty { <tr><td colspan="4" class="muted">No payments in this period.</td></tr> }
              </tbody>
            </table>
          </section>
        </div>
        <div nz-col [nzXs]="24" [nzLg]="12">
          <section class="panel" data-testid="pos-top-items">
            <h3>Top items</h3>
            <table>
              <thead><tr><th>Item</th><th class="amt">Sold</th><th class="amt">Revenue</th></tr></thead>
              <tbody>
                @for (i of r?.topItems ?? []; track i.itemCode + i.description) {
                  <tr><td>{{ i.description }}</td><td class="amt">{{ i.quantity | number:'1.0-2' }}</td><td class="amt strong">{{ i.revenue | number:'1.2-2' }}</td></tr>
                } @empty { <tr><td colspan="3" class="muted">Nothing sold in this period.</td></tr> }
              </tbody>
            </table>
          </section>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <ng-container *ngTemplateOutlet="group; context: { title: 'By register', rows: r?.registers ?? [], id: 'pos-by-register' }"></ng-container>
        </div>
        <div nz-col [nzXs]="24" [nzLg]="12">
          <ng-container *ngTemplateOutlet="group; context: { title: 'By cashier', rows: r?.cashiers ?? [], id: 'pos-by-cashier' }"></ng-container>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="14">
          <section class="panel" data-testid="pos-open-shifts">
            <div class="head">
              <h3>Open shifts</h3>
              <a nz-button nzType="link" nzSize="small" routerLink="/pos/sessions">All shifts</a>
            </div>
            @if (shiftsFailed()) {
              <p class="muted">The open shifts could not be loaded.</p>
              <button nz-button nzSize="small" type="button" (click)="loadShifts()">Try again</button>
            } @else if (shifts(); as rows) {
              @for (s of rows; track s.id) {
                <div class="shift">
                  <div class="who"><strong>{{ s.registerName }}</strong><span class="muted">{{ s.cashierName }} · since {{ s.openedAt | erpDate }}</span></div>
                  <div class="cash"><span class="muted">Expected cash</span><strong>{{ currency() }} {{ s.expectedCash | number:'1.2-2' }}</strong></div>
                </div>
              } @empty {
                <p class="muted">No shift is open. Open one from the terminal to start selling.</p>
                <a nz-button routerLink="/pos/terminal"><span nz-icon nzType="shop"></span>Open Terminal</a>
              }
            } @else {
              <nz-skeleton [nzActive]="true" [nzTitle]="false" [nzParagraph]="{ rows: 3 }" />
            }
          </section>
        </div>
        <div nz-col [nzXs]="24" [nzLg]="10">
          <erp-status-counts title="All orders by status" link="/pos/orders" linkLabel="Open orders" emptyText="No counter orders yet."
            [rows]="orders()" [failed]="countsFailed()" (retry)="loadCounts()" testId="pos-order-counts" />
        </div>
      </div>
    }

    <ng-template #group let-title="title" let-rows="rows" let-id="id">
      <section class="panel" [attr.data-testid]="id">
        <h3>{{ title }}</h3>
        <table>
          <thead><tr><th>Name</th><th class="amt">Sales</th><th class="amt">Count</th><th class="amt">Returns</th><th class="amt">Net</th></tr></thead>
          <tbody>
            @for (g of asGroups(rows); track g.name) {
              <tr><td>{{ g.name }}</td><td class="amt">{{ g.sales | number:'1.2-2' }}</td><td class="amt">{{ g.count }}</td><td class="amt">{{ g.returns | number:'1.2-2' }}</td><td class="amt strong">{{ g.net | number:'1.2-2' }}</td></tr>
            } @empty { <tr><td colspan="5" class="muted">Nothing in this period.</td></tr> }
          </tbody>
        </table>
      </section>
    </ng-template>
  `,
  styles: [`
    .toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin-bottom: 16px; }
    .ranges { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; flex: 1; }
    .register { width: 200px; }
    .six { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
    .grid-row { margin-top: 16px; }
    .panel { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); height: 100%; padding: 16px 20px; border-radius: var(--erp-radius-card, 8px); }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 12px; }
    h3 { margin: 0 0 8px; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .head h3 { margin: 0; }
    .muted { color: var(--erp-color-text-muted); }
    p.muted { margin: 0 0 8px; }
    table { width: 100%; border-collapse: collapse; }
    th { font-size: 12px; font-weight: 600; text-align: start; color: var(--erp-color-text-muted); }
    th.amt { text-align: end; }
    td, th { padding: 5px 8px 5px 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .amt { text-align: end; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .strong { font-weight: 700; }
    .shift { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 8px 0; border-top: 1px solid var(--erp-color-border-subtle); }
    .who, .cash { display: flex; flex-direction: column; min-width: 0; }
    .cash { text-align: end; font-variant-numeric: tabular-nums; white-space: nowrap; }
  `],
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
