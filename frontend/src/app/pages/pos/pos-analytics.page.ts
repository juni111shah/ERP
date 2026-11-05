import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { PosKpiApi, PosKpiSummaryDto, PosOrdersApi, PosSessionSummaryDto, PosSessionsApi } from '../../core/api/pos.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { countByStatus, StatusCount, StatusCountsComponent } from '../sales/status-counts.component';

const ORDER_STATUSES = ['completed', 'parked', 'voided'];

@Component({
  selector: 'erp-pos-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, NzGridModule, NzButtonModule, NzIconModule, NzSkeletonModule, ErpDatePipe, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  template: `
    <h1 class="erp-page-title">Point of Sale Analytics & Insights</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="pos-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The point of sale figures could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadMetrics()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="pos-analytics-stats">
        <erp-stat-card
          label="Today's Sales"
          [value]="kpis()?.todaySales ?? 0"
          format="money"
          caption="completed orders, all registers"
          [loading]="loading()"
          [tone]="0" />

        <erp-stat-card
          label="Orders Today"
          [value]="kpis()?.totalOrdersToday ?? 0"
          format="number"
          caption="completed, voids left out"
          [loading]="loading()"
          [tone]="1" />

        <erp-stat-card
          label="Average Ticket"
          [value]="kpis()?.averageTicket ?? 0"
          format="money"
          caption="per completed order today"
          [loading]="loading()"
          [tone]="4" />

        <erp-stat-card
          label="Open Shifts"
          [value]="kpis()?.openSessionsCount ?? 0"
          format="number"
          [caption]="'with ' + (kpis()?.activeRegistersCount ?? 0) + ' active registers'"
          [loading]="loading()"
          [tone]="3" />
      </div>

      <div nz-row [nzGutter]="[16, 16]" class="insights-row">
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
  `,
  styles: [`
    .analytics-toolbar { display: flex; justify-content: flex-end; align-items: center; margin-bottom: 16px; }
    .insights-row { margin-top: 16px; }
    .panel { height: 100%; padding: 16px 20px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: var(--erp-radius-card, 8px); }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 12px; }
    h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .muted { color: var(--erp-color-text-muted); }
    p.muted { margin: 0 0 8px; }
    .shift { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 8px 0; border-top: 1px solid var(--erp-color-border-subtle); }
    .who, .cash { display: flex; flex-direction: column; min-width: 0; }
    .cash { text-align: end; font-variant-numeric: tabular-nums; white-space: nowrap; }
  `],
})
export class PosAnalyticsPage {
  private readonly kpiApi = inject(PosKpiApi);
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly kpis = signal<PosKpiSummaryDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly orders = signal<StatusCount[] | null>(null);
  readonly countsFailed = signal(false);
  readonly shifts = signal<PosSessionSummaryDto[] | null>(null);
  readonly shiftsFailed = signal(false);

  constructor() {
    this.loadMetrics();
  }

  loadMetrics(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.kpiApi.getSummary().subscribe({
      next: (k) => {
        this.kpis.set(k);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
    this.loadCounts();
    this.loadShifts();
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
