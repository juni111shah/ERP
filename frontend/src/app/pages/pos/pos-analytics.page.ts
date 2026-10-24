import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PosKpiApi, PosKpiSummaryDto } from '../../core/api/pos.api';

@Component({
  selector: 'erp-pos-analytics',
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
          <h1 class="erp-page-title">Point of Sale Analytics & Insights</h1>
          <p class="erp-page-subtitle">Counter sales performance, register throughput, average transaction values and shift metrics.</p>
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
              <span class="stat-label">Today's POS Sales</span>
              <span nz-icon nzType="dollar" class="stat-icon icon-sales"></span>
            </div>
            <div class="stat-value">AED {{ kpis()?.todaySales ?? 0 | number:'1.2-2' }}</div>
            <div class="stat-sub">across all counter registers</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Orders Completed Today</span>
              <span nz-icon nzType="shopping-cart" class="stat-icon icon-orders"></span>
            </div>
            <div class="stat-value">{{ kpis()?.totalOrdersToday ?? 0 }}</div>
            <div class="stat-sub">customer retail transactions</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Average Ticket Size</span>
              <span nz-icon nzType="rise" class="stat-icon icon-ticket"></span>
            </div>
            <div class="stat-value">AED {{ kpis()?.averageTicket ?? 0 | number:'1.2-2' }}</div>
            <div class="stat-sub">average spend per counter customer</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Active Shifts Open</span>
              <span nz-icon nzType="schedule" class="stat-icon icon-shifts"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openSessionsCount ?? 0 }}</div>
            <div class="stat-sub">of {{ kpis()?.activeRegistersCount ?? 0 }} total registered tills</div>
          </nz-card>
        </div>
      </div>

      <!-- POS Workflow Guidance & Shortcuts -->
      <div nz-row [nzGutter]="16">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Retail Operations Hub" class="detail-card">
            <div class="stat-breakdown-list">
              <div class="breakdown-item">
                <span class="label">Total Registered Cash Tills:</span>
                <span class="val">{{ kpis()?.activeRegistersCount ?? 0 }} active</span>
              </div>
              <div class="breakdown-item">
                <span class="label">Currently Open Shifts:</span>
                <span class="val">{{ kpis()?.openSessionsCount ?? 0 }} shifts</span>
              </div>
              <div class="breakdown-item">
                <span class="label">Average Transaction Value:</span>
                <span class="val">AED {{ kpis()?.averageTicket ?? 0 | number:'1.2-2' }}</span>
              </div>
            </div>

            <div class="quick-links" style="margin-top: 24px; display: flex; gap: 8px; flex-wrap: wrap;">
              <a routerLink="/pos/terminal" nz-button nzType="primary">
                <span nz-icon nzType="shop"></span>Launch Terminal
              </a>
              <a routerLink="/pos/sessions" nz-button nzType="default">
                <span nz-icon nzType="schedule"></span>Manage Shifts
              </a>
              <a routerLink="/pos/orders" nz-button nzType="default">
                <span nz-icon nzType="file-text"></span>Order History
              </a>
              <a routerLink="/pos/registers" nz-button nzType="default">
                <span nz-icon nzType="desktop"></span>Configure Tills
              </a>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Counter Cash Cycle" class="detail-card">
            <div class="flow-steps">
              <div class="step-card">
                <div class="step-num">1</div>
                <div class="step-content">
                  <strong>Shift Float Opening</strong>
                  <p>Cashier counts physical start float, enters amount, and opens register shift.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">2</div>
                <div class="step-content">
                  <strong>Fast Counter Checkout</strong>
                  <p>Scan barcodes or tap product grid, calculate VAT, take cash or card payments.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">3</div>
                <div class="step-content">
                  <strong>Midday Safe Drops</strong>
                  <p>Safely transfer surplus cash from till to back-office vault with reason log.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">4</div>
                <div class="step-content">
                  <strong>End of Shift Reconciliation</strong>
                  <p>Reconcile physical cash vs expected drawer cash, calculating overage/shortage variance.</p>
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
    .icon-sales { color: #10955d; background: #ecfdf5; }
    .icon-orders { color: #2563eb; background: #eff6ff; }
    .icon-ticket { color: #8b5cf6; background: #f5f3ff; }
    .icon-shifts { color: #d97706; background: #fffbeb; }
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
export class PosAnalyticsPage {
  private readonly kpiApi = inject(PosKpiApi);
  readonly kpis = signal<PosKpiSummaryDto | null>(null);

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
