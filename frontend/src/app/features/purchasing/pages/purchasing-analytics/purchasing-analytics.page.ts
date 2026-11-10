import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PurchaseOrdersApi, PurchaseRequisitionsApi, PurchasingKpiApi, PurchasingKpiDto } from '@core/api/purchasing.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { countByStatus, StatusCount, StatusCountsComponent } from '@features/sales/components/status-counts/status-counts.component';

const ORDER_STATUSES = ['draft', 'submitted', 'approved', 'partiallyReceived', 'received', 'billed', 'cancelled'];
const REQUISITION_STATUSES = ['draft', 'pendingApproval', 'approved', 'rejected', 'ordered'];

@Component({
  selector: 'erp-purchasing-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzGridModule, NzButtonModule, NzIconModule, StatCardComponent, StatePanelComponent, StatusCountsComponent],
  template: `
    <h1 class="erp-page-title">Purchasing & Procurement Analytics</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="purchasing-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The purchasing figures could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadMetrics()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="purchasing-analytics-stats">
        <erp-stat-card
          label="Total Ordered"
          [value]="kpis()?.totalSpend ?? 0"
          format="money"
          caption="all orders except cancelled"
          [loading]="loading()"
          [tone]="0" />

        <erp-stat-card
          label="Open Order Value"
          [value]="kpis()?.openOrdersAmount ?? 0"
          format="money"
          [caption]="(kpis()?.openOrdersCount ?? 0) + ' open orders, including drafts'"
          [loading]="loading()"
          [tone]="3" />

        <erp-stat-card
          label="Pending Receipts"
          [value]="kpis()?.pendingReceiptsCount ?? 0"
          format="number"
          caption="approved, not fully received"
          [loading]="loading()"
          [tone]="1" />

        <erp-stat-card
          label="Open Requisitions"
          [value]="kpis()?.openRequisitionsCount ?? 0"
          format="number"
          caption="draft or waiting for approval"
          [loading]="loading()"
          [tone]="4" />
      </div>

      <div nz-row [nzGutter]="[16, 16]" class="insights-row">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <erp-status-counts title="Requisitions by status" link="/purchasing/requisitions" linkLabel="Open requisitions" emptyText="No requisitions yet."
            [rows]="requisitions()" [failed]="countsFailed()" (retry)="loadCounts()" testId="purchasing-requisition-counts" />
        </div>
        <div nz-col [nzXs]="24" [nzLg]="12">
          <erp-status-counts title="Purchase orders by status" link="/purchasing/orders" linkLabel="Open purchase orders" emptyText="No purchase orders yet."
            [rows]="orders()" [failed]="countsFailed()" (retry)="loadCounts()" testId="purchasing-order-counts" />
        </div>
      </div>
    }
  `,
  styles: [`
    .analytics-toolbar { display: flex; justify-content: flex-end; align-items: center; margin-bottom: 16px; }
    .insights-row { margin-top: 16px; }
  `],
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
