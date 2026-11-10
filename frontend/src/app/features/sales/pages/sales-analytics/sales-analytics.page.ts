import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SalesKpiApi, SalesKpiDto, SalesOrdersApi, SalesQuotesApi } from '@core/api/sales.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { countByStatus, StatusCount, StatusCountsComponent } from '../../components/status-counts/status-counts.component';

const QUOTE_STATUSES = ['draft', 'sent', 'accepted', 'declined', 'expired', 'converted'];
const ORDER_STATUSES = ['draft', 'confirmed', 'processing', 'delivered', 'invoiced', 'cancelled'];

@Component({
  selector: 'erp-sales-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzGridModule, NzButtonModule, NzIconModule, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  template: `
    <h1 class="erp-page-title">Sales Analytics & Performance</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="sales-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The sales figures could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadMetrics()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="sales-analytics-stats">
        <erp-stat-card
          label="Total Booked"
          [value]="kpis()?.totalSales ?? 0"
          format="money"
          caption="all orders except cancelled"
          [loading]="loading()"
          [tone]="0" />

        <erp-stat-card
          label="Order Backlog"
          [value]="kpis()?.openOrdersAmount ?? 0"
          format="money"
          [caption]="(kpis()?.openOrdersCount ?? 0) + ' confirmed or processing'"
          [loading]="loading()"
          [tone]="3" />

        <erp-stat-card
          label="Open Quote Value"
          [value]="kpis()?.openQuotesAmount ?? 0"
          format="money"
          [caption]="(kpis()?.openQuotesCount ?? 0) + ' draft or sent'"
          [loading]="loading()"
          [tone]="1" />

        <erp-stat-card
          label="Quotes Converted"
          [value]="kpis()?.conversionRate ?? 0"
          format="percent"
          [caption]="'of ' + (kpis()?.totalQuotesCount ?? 0) + ' quotes became orders'"
          [loading]="loading()"
          [tone]="4" />
      </div>

      <div nz-row [nzGutter]="[16, 16]" class="insights-row">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <erp-status-counts title="Quotes by status" link="/sales/quotes" linkLabel="Open quotes" emptyText="No quotes yet."
            [rows]="quotes()" [failed]="countsFailed()" [note]="waitingNote()" (retry)="loadCounts()" testId="sales-quote-counts" />
        </div>
        <div nz-col [nzXs]="24" [nzLg]="12">
          <erp-status-counts title="Orders by status" link="/sales/orders" linkLabel="Open orders" emptyText="No orders yet."
            [rows]="orders()" [failed]="countsFailed()" (retry)="loadCounts()" testId="sales-order-counts" />
        </div>
      </div>
    }
  `,
  styles: [`
    .analytics-toolbar { display: flex; justify-content: flex-end; align-items: center; margin-bottom: 16px; }
    .insights-row { margin-top: 16px; }
  `],
})
export class SalesAnalyticsPage {
  private readonly kpiApi = inject(SalesKpiApi);
  private readonly quotesApi = inject(SalesQuotesApi);
  private readonly ordersApi = inject(SalesOrdersApi);

  readonly kpis = signal<SalesKpiDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly quotes = signal<StatusCount[] | null>(null);
  readonly orders = signal<StatusCount[] | null>(null);
  readonly countsFailed = signal(false);

  readonly waitingNote = computed(() => {
    const accepted = this.quotes()?.find((r) => r.value === 'accepted')?.count ?? 0;
    if (!accepted) return null;
    return accepted === 1 ? '1 accepted quote has not been converted to an order.' : `${accepted} accepted quotes have not been converted to orders.`;
  });

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
  }

  loadCounts(): void {
    this.countsFailed.set(false);
    countByStatus((q) => this.quotesApi.list(q), QUOTE_STATUSES).subscribe((rows) => {
      this.quotes.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
    countByStatus((q) => this.ordersApi.list(q), ORDER_STATUSES).subscribe((rows) => {
      this.orders.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
  }
}
