import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PurchaseOrdersApi, PurchaseRequisitionsApi, PurchasingKpiApi, PurchasingKpiDto } from '@core/api/purchasing.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusCount, StatusCountsComponent, countByStatus } from '@features/sales/components/status-counts/status-counts.component';

const ORDER_STATUSES = ['draft', 'submitted', 'approved', 'partiallyReceived', 'received', 'billed', 'cancelled'];
const REQUISITION_STATUSES = ['draft', 'pendingApproval', 'approved', 'rejected', 'ordered'];

/** Purchasing analytics: the KPI cards and how many orders and requisitions sit in each status. */
@Component({
  selector: 'erp-purchasing-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzGridModule, NzIconModule, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  templateUrl: './purchasing-analytics.page.html',
  styleUrl: './purchasing-analytics.page.scss',
})
export class PurchasingAnalyticsPage {
  private readonly kpiApi = inject(PurchasingKpiApi);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly requisitionsApi = inject(PurchaseRequisitionsApi);

  readonly kpis = signal<PurchasingKpiDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly orders = signal<StatusCount[] | null>(null);
  readonly requisitions = signal<StatusCount[] | null>(null);
  readonly countsFailed = signal(false);

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
    countByStatus((q) => this.ordersApi.list(q), ORDER_STATUSES).subscribe((rows) => {
      this.orders.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
    countByStatus((q) => this.requisitionsApi.list(q), REQUISITION_STATUSES).subscribe((rows) => {
      this.requisitions.set(rows);
      if (!rows) this.countsFailed.set(true);
    });
  }
}
