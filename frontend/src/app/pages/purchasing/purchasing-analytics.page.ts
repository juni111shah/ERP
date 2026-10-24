import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PurchasingKpiApi, PurchasingKpiDto } from '../../core/api/purchasing.api';

@Component({
  selector: 'erp-purchasing-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
  ],
  template: `
    <div class="analytics-container">
      <div class="page-header">
        <div>
          <h1 class="erp-page-title">Purchasing & Procurement Analytics</h1>
          <p class="erp-page-subtitle">Track procurement spend, outstanding vendor commitments, and goods receiving performance.</p>
        </div>
        <div class="header-actions">
          <button nz-button (click)="loadMetrics()">
            <span nz-icon nzType="reload"></span>Refresh
          </button>
        </div>
      </div>

      <!-- Main KPI Cards -->
      <div nz-row [nzGutter]="[16, 16]" class="kpi-row">
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Total Spend</span>
              <span nz-icon nzType="dollar" class="stat-icon icon-spend"></span>
            </div>
            <div class="stat-value">{{ kpis()?.totalSpend ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">lifetime procurement spend</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Committed Backlog</span>
              <span nz-icon nzType="clock-circle" class="stat-icon icon-backlog"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openOrdersAmount ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">{{ kpis()?.openOrdersCount ?? 0 }} purchase orders open</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Pending Receipts</span>
              <span nz-icon nzType="inbox" class="stat-icon icon-receipts"></span>
            </div>
            <div class="stat-value">{{ kpis()?.pendingReceiptsCount ?? 0 }}</div>
            <div class="stat-sub">orders awaiting inbound GRN</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Open Requisitions</span>
              <span nz-icon nzType="audit" class="stat-icon icon-reqs"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openRequisitionsCount ?? 0 }}</div>
            <div class="stat-sub">purchase requests pending approval</div>
          </nz-card>
        </div>
      </div>

      <!-- Procurement Workflow & Operational Guidance -->
      <div nz-row [nzGutter]="16">
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

            <div class="quick-links" style="margin-top: 24px; display: flex; gap: 8px; flex-wrap: wrap;">
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
    </div>
  `,
  styles: [`
    .analytics-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .erp-page-title { font-size: 24px; font-weight: 700; color: #0f172a; margin: 0; }
    .erp-page-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .kpi-row { margin-bottom: 24px; }
    .stat-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
      transition: all 0.2s;
    }
    .stat-card:hover { transform: translateY(-2px); box-shadow: 0 8px 16px -4px rgba(0,0,0,0.06); }
    .stat-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    .stat-label { font-size: 13px; font-weight: 600; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; }
    .stat-icon { font-size: 18px; padding: 6px; border-radius: 8px; }
    .icon-spend { color: #10955d; background: #ecfdf5; }
    .icon-backlog { color: #d97706; background: #fffbeb; }
    .icon-receipts { color: #2563eb; background: #eff6ff; }
    .icon-reqs { color: #8b5cf6; background: #f5f3ff; }
    .stat-value { font-size: 26px; font-weight: 700; color: #0f172a; line-height: 1.2; }
    .stat-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .detail-card { border-radius: 12px; border: 1px solid #e2e8f0; height: 100%; }
    .stat-breakdown-list { padding-top: 14px; display: flex; flex-direction: column; gap: 10px; }
    .breakdown-item { display: flex; justify-content: space-between; font-size: 14px; color: #475569; }
    .breakdown-item .val { font-weight: 600; color: #0f172a; }
    .flow-steps { display: flex; flex-direction: column; gap: 12px; margin-top: 16px; }
    .step-card { display: flex; gap: 14px; align-items: flex-start; background: #f8fafc; border-radius: 8px; padding: 12px; border: 1px solid #f1f5f9; }
    .step-num { width: 28px; height: 28px; border-radius: 50%; background: #2563eb; color: white; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 13px; flex-shrink: 0; }
    .step-content strong { font-size: 14px; color: #0f172a; }
    .step-content p { margin: 2px 0 0 0; font-size: 12px; color: #64748b; }
  `],
})
export class PurchasingAnalyticsPage {
  private readonly kpiApi = inject(PurchasingKpiApi);
  readonly kpis = signal<PurchasingKpiDto | null>(null);

  constructor() {
    this.loadMetrics();
  }

  loadMetrics(): void {
    this.kpiApi.getSummary().subscribe({
      next: (k) => this.kpis.set(k),
      error: () => {},
    });
  }
}
