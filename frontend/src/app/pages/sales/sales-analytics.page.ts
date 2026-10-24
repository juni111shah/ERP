import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { SalesKpiApi, SalesKpiDto } from '../../core/api/sales.api';

@Component({
  selector: 'erp-sales-analytics',
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
    NzProgressModule,
  ],
  template: `
    <div class="analytics-container">
      <div class="page-header">
        <div>
          <h1 class="erp-page-title">Sales Analytics & Performance</h1>
          <p class="erp-page-subtitle">Commercial order volumes, quotation conversion metrics and pipeline health.</p>
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
              <span class="stat-label">Total Revenue Booked</span>
              <span nz-icon nzType="dollar" class="stat-icon icon-rev"></span>
            </div>
            <div class="stat-value">{{ kpis()?.totalSales ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">lifetime commercial sales</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Order Backlog</span>
              <span nz-icon nzType="clock-circle" class="stat-icon icon-backlog"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openOrdersAmount ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">{{ kpis()?.openOrdersCount ?? 0 }} active orders in flight</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Pipeline Value</span>
              <span nz-icon nzType="file-text" class="stat-icon icon-pipe"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openQuotesAmount ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">{{ kpis()?.openQuotesCount ?? 0 }} quotes outstanding</div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Conversion Rate</span>
              <span nz-icon nzType="rise" class="stat-icon icon-conv"></span>
            </div>
            <div class="stat-value">{{ kpis()?.conversionRate ?? 0 }}%</div>
            <div class="stat-sub">quote to confirmed order</div>
          </nz-card>
        </div>
      </div>

      <!-- Performance Insights Breakdown -->
      <div nz-row [nzGutter]="16">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Pipeline Conversion Health" class="detail-card">
            <div class="progress-section">
              <div class="progress-head">
                <span>Quote Acceptance Rate</span>
                <span><strong>{{ kpis()?.conversionRate ?? 0 }}%</strong></span>
              </div>
              <nz-progress [nzPercent]="kpis()?.conversionRate ?? 0" [nzStrokeColor]="'#10955d'"></nz-progress>
            </div>

            <div class="stat-breakdown-list">
              <div class="breakdown-item">
                <span class="label">Total Quotations Drafted & Issued:</span>
                <span class="val">{{ kpis()?.totalQuotesCount ?? 0 }}</span>
              </div>
              <div class="breakdown-item">
                <span class="label">Active Proposals Pending Client:</span>
                <span class="val">{{ kpis()?.openQuotesCount ?? 0 }}</span>
              </div>
              <div class="breakdown-item">
                <span class="label">Total Confirmed Sales Orders:</span>
                <span class="val">{{ kpis()?.totalOrdersCount ?? 0 }}</span>
              </div>
            </div>

            <div class="quick-links" style="margin-top: 24px;">
              <a routerLink="/sales/quotes" nz-button nzType="default">
                <span nz-icon nzType="file-search"></span>Inspect All Quotes
              </a>
              <a routerLink="/sales/orders" nz-button nzType="primary" style="margin-left: 8px;">
                <span nz-icon nzType="shopping-cart"></span>Manage Orders
              </a>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Commercial Flow Overview" class="detail-card">
            <p style="color: #64748b; font-size: 14px; line-height: 1.6;">
              The standard enterprise sales lifecycle tracks opportunities from estimates to confirmed sales orders, fulfillment deliveries, and core accounting invoices.
            </p>

            <div class="flow-steps">
              <div class="step-card">
                <div class="step-num">1</div>
                <div class="step-content">
                  <strong>Sales Quotation</strong>
                  <p>Issue pricing proposals with customized items, tax rates, and discount tiers.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">2</div>
                <div class="step-content">
                  <strong>Order Confirmation</strong>
                  <p>Convert accepted quotes into sales orders with scheduled delivery dates.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">3</div>
                <div class="step-content">
                  <strong>Fulfillment & Dispatch</strong>
                  <p>Log dispatched units, monitor backorders, and track delivery notes.</p>
                </div>
              </div>
              <div class="step-card">
                <div class="step-num">4</div>
                <div class="step-content">
                  <strong>Automated Invoicing</strong>
                  <p>One-click invoice creation posted directly to core financial ledgers.</p>
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
    .icon-rev { color: #10955d; background: #ecfdf5; }
    .icon-backlog { color: #d97706; background: #fffbeb; }
    .icon-pipe { color: #2563eb; background: #eff6ff; }
    .icon-conv { color: #8b5cf6; background: #f5f3ff; }
    .stat-value { font-size: 26px; font-weight: 700; color: #0f172a; line-height: 1.2; }
    .stat-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .detail-card { border-radius: 12px; border: 1px solid #e2e8f0; height: 100%; }
    .progress-section { margin-bottom: 20px; }
    .progress-head { display: flex; justify-content: space-between; font-size: 13px; color: #475569; margin-bottom: 6px; }
    .stat-breakdown-list { border-top: 1px solid #f1f5f9; padding-top: 14px; display: flex; flex-direction: column; gap: 10px; }
    .breakdown-item { display: flex; justify-content: space-between; font-size: 14px; color: #475569; }
    .breakdown-item .val { font-weight: 600; color: #0f172a; }
    .flow-steps { display: flex; flex-direction: column; gap: 12px; margin-top: 16px; }
    .step-card { display: flex; gap: 14px; align-items: flex-start; background: #f8fafc; border-radius: 8px; padding: 12px; border: 1px solid #f1f5f9; }
    .step-num { width: 28px; height: 28px; border-radius: 50%; background: #10955d; color: white; display: flex; align-items: center; justify-content: center; font-weight: 700; font-size: 13px; flex-shrink: 0; }
    .step-content strong { font-size: 14px; color: #0f172a; }
    .step-content p { margin: 2px 0 0 0; font-size: 12px; color: #64748b; }
  `],
})
export class SalesAnalyticsPage {
  private readonly kpiApi = inject(SalesKpiApi);
  readonly kpis = signal<SalesKpiDto | null>(null);

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
