import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PosKpiApi, PosKpiSummaryDto } from '../../core/api/pos.api';
import { StatCardComponent } from '../../shared/stat-card.component';

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
    StatCardComponent,
  ],
  template: `
    <h1 class="erp-page-title">Point of Sale Analytics & Insights</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadMetrics()" [nzLoading]="loading()" data-testid="pos-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    <!-- Main KPI Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="pos-analytics-stats">
      <erp-stat-card
        label="Today's POS Sales"
        [value]="kpis()?.todaySales ?? 0"
        format="money"
        caption="across all counter registers"
        [loading]="loading()"
        [tone]="0" />

      <erp-stat-card
        label="Orders Completed Today"
        [value]="kpis()?.totalOrdersToday ?? 0"
        format="number"
        caption="customer retail transactions"
        [loading]="loading()"
        [tone]="1" />

      <erp-stat-card
        label="Average Ticket Size"
        [value]="kpis()?.averageTicket ?? 0"
        format="money"
        caption="average spend per counter customer"
        [loading]="loading()"
        [tone]="4" />

      <erp-stat-card
        label="Active Shifts Open"
        [value]="kpis()?.openSessionsCount ?? 0"
        format="number"
        [caption]="'of ' + (kpis()?.activeRegistersCount ?? 0) + ' total registered tills'"
        [loading]="loading()"
        [tone]="3" />
    </div>

    <!-- POS Workflow Guidance & Shortcuts -->
    <div nz-row [nzGutter]="[16, 16]" class="insights-row">
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
export class PosAnalyticsPage {
  private readonly kpiApi = inject(PosKpiApi);
  readonly kpis = signal<PosKpiSummaryDto | null>(null);
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
