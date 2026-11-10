import { ChangeDetectionStrategy, Component, WritableSignal, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SalesKpiApi, SalesKpiDto, SalesOrdersApi, SalesQuotesApi } from '@core/api/sales.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusCount, StatusCountsComponent, countByStatus } from '../../components/status-counts/status-counts.component';
import { ORDER_STATUSES, QUOTE_STATUSES } from '../../sales-status';

/** Sales at a glance: the KPI cards and how many quotes and orders are in each status. */
@Component({
  selector: 'erp-sales-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzGridModule, NzButtonModule, NzIconModule, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  templateUrl: './sales-analytics.page.html',
  styleUrl: './sales-analytics.page.scss',
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
    this.fillCounts(this.quotes, countByStatus((q) => this.quotesApi.list(q), QUOTE_STATUSES));
    this.fillCounts(this.orders, countByStatus((q) => this.ordersApi.list(q), ORDER_STATUSES));
  }

  /** Shows the counts; when they could not be loaded the panels say so. */
  private fillCounts(target: WritableSignal<StatusCount[] | null>, counts$: Observable<StatusCount[] | null>): void {
    counts$.subscribe((rows) => {
      target.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
  }
}
