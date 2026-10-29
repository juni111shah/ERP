import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { SalesKpiApi, SalesKpiDto } from '../../core/api/sales.api';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-sales-analytics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzProgressModule,
    StatCardComponent,
  ],
  template: `
    <h1 class="erp-page-title">Sales Analytics & Performance</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="sales-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    <!-- Main KPI Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="sales-analytics-stats">
      <erp-stat-card
        label="Total Revenue Booked"
        [value]="kpis()?.totalSales ?? 0"
        format="money"
        caption="lifetime commercial sales"
        [loading]="loading()"
        [tone]="0" />

      <erp-stat-card
        label="Order Backlog"
        [value]="kpis()?.openOrdersAmount ?? 0"
        format="money"
        [caption]="(kpis()?.openOrdersCount ?? 0) + ' active orders in flight'"
        [loading]="loading()"
        [tone]="3" />

      <erp-stat-card
        label="Pipeline Value"
        [value]="kpis()?.openQuotesAmount ?? 0"
        format="money"
        [caption]="(kpis()?.openQuotesCount ?? 0) + ' quotes outstanding'"
        [loading]="loading()"
        [tone]="1" />

      <erp-stat-card
        label="Conversion Rate"
        [value]="kpis()?.conversionRate ?? 0"
        format="percent"
        caption="quote to confirmed order"
        [loading]="loading()"
        [tone]="4" />
    </div>

    <!-- Performance Insights Breakdown -->
    <div nz-row [nzGutter]="[16, 16]" class="insights-row">
      <div nz-col [nzXs]="24" [nzLg]="12">
        <nz-card nzTitle="Pipeline Conversion Health" class="detail-card">
          <div class="progress-section">
            <div class="progress-head">
              <span>Quote Acceptance Rate</span>
              <span class="rate-val"><strong>{{ kpis()?.conversionRate ?? 0 }}%</strong></span>
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

          <div class="quick-links">
            <a routerLink="/sales/quotes" nz-button nzType="default">
              <span nz-icon nzType="file-search"></span>Inspect All Quotes
            </a>
            <a routerLink="/sales/orders" nz-button nzType="primary">
              <span nz-icon nzType="shopping-cart"></span>Manage Orders
            </a>
          </div>
        </nz-card>
      </div>

      <div nz-col [nzXs]="24" [nzLg]="12">
        <nz-card nzTitle="Commercial Flow Overview" class="detail-card">
          <p class="flow-desc">
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
    .progress-section {
      margin-bottom: 20px;
    }
    .progress-head {
      display: flex;
      justify-content: space-between;
      font-size: 13px;
      color: #475569;
      margin-bottom: 6px;
    }
    .rate-val {
      color: #10955d;
    }
    .stat-breakdown-list {
      border-top: 1px solid #f1f5f9;
      padding-top: 14px;
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
      gap: 12px;
      margin-top: 24px;
      padding-top: 16px;
      border-top: 1px solid #f1f5f9;
    }
    .flow-desc {
      color: #64748b;
      font-size: 13px;
      line-height: 1.6;
      margin-bottom: 16px;
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
      background: #10955d;
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 12px;
      flex-shrink: 0;
      box-shadow: 0 2px 4px rgba(16, 149, 93, 0.25);
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
export class SalesAnalyticsPage {
  private readonly kpiApi = inject(SalesKpiApi);
  readonly kpis = signal<SalesKpiDto | null>(null);
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

