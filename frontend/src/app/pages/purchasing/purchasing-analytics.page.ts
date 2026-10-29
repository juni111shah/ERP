import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PurchasingKpiApi, PurchasingKpiDto } from '../../core/api/purchasing.api';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-purchasing-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    StatCardComponent,
  ],
  template: `
    <h1 class="erp-page-title">Purchasing & Procurement Analytics</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="purchasing-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    <!-- Main KPI Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="purchasing-analytics-stats">
      <erp-stat-card
        label="Total Spend"
        [value]="kpis()?.totalSpend ?? 0"
        format="money"
        caption="lifetime procurement spend"
        [loading]="loading()"
        [tone]="0" />

      <erp-stat-card
        label="Committed Backlog"
        [value]="kpis()?.openOrdersAmount ?? 0"
        format="money"
        [caption]="(kpis()?.openOrdersCount ?? 0) + ' purchase orders open'"
        [loading]="loading()"
        [tone]="3" />

      <erp-stat-card
        label="Pending Receipts"
        [value]="kpis()?.pendingReceiptsCount ?? 0"
        format="number"
        caption="orders awaiting inbound GRN"
        [loading]="loading()"
        [tone]="1" />

      <erp-stat-card
        label="Open Requisitions"
        [value]="kpis()?.openRequisitionsCount ?? 0"
        format="number"
        caption="purchase requests pending approval"
        [loading]="loading()"
        [tone]="4" />
    </div>

    <!-- Procurement Workflow & Operational Guidance -->
    <div nz-row [nzGutter]="[16, 16]" class="insights-row">
      <div nz-col [nzXs]="24" [nzLg]="12">
        <nz-card nzTitle="Procurement Operations" class="detail-card">
          <div class="stat-breakdown-list">
            <div class="breakdown-item">
              <span class="label">Total Purchase Orders Issued:</span>
              <span class="val">{{ kpis()?.totalOrdersCount ?? 0 }}</span>
            </div>
            <div class="breakdown-item">
              <span class="label">Open Orders Awaiting Delivery:</span>
              <span class="val">{{ kpis()?.openOrdersCount ?? 0 }}</span>
            </div>
            <div class="breakdown-item">
              <span class="label">Requisitions Pending Review:</span>
              <span class="val">{{ kpis()?.openRequisitionsCount ?? 0 }}</span>
            </div>
          </div>

          <div class="quick-links">
            <a routerLink="/purchasing/orders" nz-button nzType="primary">
              <span nz-icon nzType="shopping"></span>Purchase Orders
            </a>
            <a routerLink="/purchasing/requisitions" nz-button nzType="default">
              <span nz-icon nzType="file-text"></span>Requisitions
            </a>
            <a routerLink="/purchasing/receipts" nz-button nzType="default">
              <span nz-icon nzType="inbox"></span>Goods Receipts
            </a>
          </div>
        </nz-card>
      </div>

      <div nz-col [nzXs]="24" [nzLg]="12">
        <nz-card nzTitle="Procurement Lifecycle" class="detail-card">
          <div class="flow-steps">
            <div class="step-card">
              <div class="step-num">1</div>
              <div class="step-content">
                <strong>Requisition & Approval</strong>
                <p>Departmental staff request equipment, materials, or services for budget approval.</p>
              </div>
            </div>
            <div class="step-card">
              <div class="step-num">2</div>
              <div class="step-content">
                <strong>Purchase Order Creation</strong>
                <p>Issue formal orders to vendors with negotiated pricing, terms, and delivery schedules.</p>
              </div>
            </div>
            <div class="step-card">
              <div class="step-num">3</div>
              <div class="step-content">
                <strong>Goods Receipt Note (GRN)</strong>
                <p>Warehouse verifies physical receipt, logs delivery note references, and tracks discrepancies.</p>
              </div>
            </div>
            <div class="step-card">
              <div class="step-num">4</div>
              <div class="step-content">
                <strong>Vendor Bill 3-Way Match</strong>
                <p>One-click conversion to financial vendor bills with automatic matching against receipts.</p>
              </div>
            </div>
          </div>
        </nz-card>
      </div>
    </div>
  `,
  styles: [`
    .analytics-toolbar {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      margin-bottom: 16px;
    }
    .insights-row {
      margin-top: 16px;
    }
    .detail-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
      height: 100%;
      background: #ffffff;
      
      ::ng-deep .ant-card-head {
        border-bottom: 1px solid #f1f5f9;
        font-weight: 600;
        font-size: 15px;
        color: #0f172a;
        padding: 0 20px;
        min-height: 48px;
      }
      ::ng-deep .ant-card-body {
        padding: 20px;
      }
    }
    .stat-breakdown-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .breakdown-item {
      display: flex;
      justify-content: space-between;
      font-size: 14px;
      color: #475569;
      padding: 6px 0;
      border-bottom: 1px dashed #f1f5f9;
      &:last-child {
        border-bottom: none;
      }
    }
    .breakdown-item .val {
      font-weight: 600;
      color: #0f172a;
      font-variant-numeric: tabular-nums;
    }
    .quick-links {
      display: flex;
      gap: 10px;
      margin-top: 24px;
      padding-top: 16px;
      border-top: 1px solid #f1f5f9;
      flex-wrap: wrap;
    }
    .flow-steps {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .step-card {
      display: flex;
      gap: 14px;
      align-items: flex-start;
      background: #f8fafc;
      border-radius: 8px;
      padding: 12px 14px;
      border: 1px solid #f1f5f9;
      transition: all 0.2s ease;
      &:hover {
        background: #ffffff;
        border-color: #e2e8f0;
        box-shadow: 0 2px 6px rgba(0, 0, 0, 0.04);
      }
    }
    .step-num {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: #2563eb;
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 12px;
      flex-shrink: 0;
      box-shadow: 0 2px 4px rgba(37, 99, 235, 0.25);
    }
    .step-content strong {
      font-size: 13.5px;
      color: #0f172a;
    }
    .step-content p {
      margin: 2px 0 0 0;
      font-size: 12px;
      color: #64748b;
      line-height: 1.4;
    }
  `],
})
export class PurchasingAnalyticsPage {
  private readonly kpiApi = inject(PurchasingKpiApi);
  readonly kpis = signal<PurchasingKpiDto | null>(null);
  readonly loading = signal(false);

  constructor() {
    this.loadMetrics();
  }

  loadMetrics(): void {
    this.loading.set(true);
    this.kpiApi.getSummary().subscribe({
      next: (k) => {
        this.kpis.set(k);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}

