import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ManufacturingKpiApi, ManufacturingKpisDto } from '../../core/api/manufacturing.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

@Component({
  selector: 'erp-manufacturing-analytics-page',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzGridModule,
    NzStatisticModule,
    NzIconModule,
    NzButtonModule,
    NzProgressModule,
    NzTagModule,
    NzBadgeModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Manufacturing & Shop-Floor Analytics</h2>
          <p class="text-secondary">Overview of production order fulfillment, shop-floor yield performance, and manufacturing costs</p>
        </div>
        <button nz-button nzType="default" (click)="loadKpis()">
          <span nz-icon nzType="reload"></span> Refresh KPIs
        </button>
      </div>

      <div *ngIf="kpis() as k" class="analytics-content">
        <!-- Top KPI Row -->
        <div nz-row [nzGutter]="16" class="mb-4">
          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Active BOM Recipes'"
                [nzValue]="k.activeBoms"
                [nzPrefix]="iconBoms">
              </nz-statistic>
              <ng-template #iconBoms><span nz-icon nzType="profile" class="kpi-icon text-primary"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Orders In Progress'"
                [nzValue]="k.inProgressWorkOrders"
                [nzPrefix]="iconProgress">
              </nz-statistic>
              <ng-template #iconProgress><span nz-icon nzType="sync" class="kpi-icon text-warning"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Completed Work Orders'"
                [nzValue]="k.completedWorkOrders"
                [nzPrefix]="iconDone">
              </nz-statistic>
              <ng-template #iconDone><span nz-icon nzType="check-circle" class="kpi-icon text-success"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Month Production Cost'"
                [nzValue]="(k.monthProductionCost | number:'1.2-2') || '0.00'"
                [nzPrefix]="currency() + ' '">
              </nz-statistic>
            </nz-card>
          </div>
        </div>

        <!-- Performance / Yield Row -->
        <div nz-row [nzGutter]="16">
          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Shop-Floor Yield & Scrap Rate">
              <div class="yield-box">
                <div class="yield-stat">
                  <span class="yield-val font-semibold">{{ k.yieldRatePercentage | number:'1.1-1' }}%</span>
                  <span class="text-secondary">First-Pass Yield</span>
                </div>
                <nz-progress
                  [nzPercent]="k.yieldRatePercentage"
                  [nzStrokeColor]="{ '0%': '#108ee9', '100%': '#87d068' }">
                </nz-progress>
              </div>

              <div class="units-breakdown mt-4">
                <div class="unit-item">
                  <span class="label">Total Planned Output:</span>
                  <span class="val font-medium">{{ k.totalPlannedUnits | number }}</span>
                </div>
                <div class="unit-item">
                  <span class="label">Finished Good Units:</span>
                  <span class="val text-success font-medium">{{ k.totalCompletedUnits | number }}</span>
                </div>
                <div class="unit-item">
                  <span class="label">Scrapped / Waste Units:</span>
                  <span class="val text-danger font-medium">{{ k.totalScrappedUnits | number }}</span>
                </div>
              </div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Production Pipeline Overview">
              <div class="order-pipeline">
                <div class="pipeline-row">
                  <span>Planned / Queued Orders</span>
                  <nz-tag nzColor="default">{{ k.plannedWorkOrders }}</nz-tag>
                </div>
                <div class="pipeline-row">
                  <span>In-Production / Shop-Floor</span>
                  <nz-tag nzColor="processing">{{ k.inProgressWorkOrders }}</nz-tag>
                </div>
                <div class="pipeline-row">
                  <span>Completed & Closed Orders</span>
                  <nz-tag nzColor="success">{{ k.completedWorkOrders }}</nz-tag>
                </div>
              </div>
            </nz-card>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 1.5rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .page-header h2 { margin: 0; font-size: 1.5rem; font-weight: 600; }
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-primary { color: var(--erp-primary, #0ea5e9); }
    .text-warning { color: #f59e0b; }
    .text-success { color: #10b981; }
    .text-danger { color: #ef4444; }
    .kpi-card { background: var(--erp-bg-secondary, #f8fafc); border-radius: 8px; }
    .kpi-icon { font-size: 1.25rem; margin-right: 0.5rem; }
    .yield-box { display: flex; flex-direction: column; gap: 0.75rem; }
    .yield-stat { display: flex; justify-content: space-between; align-items: baseline; }
    .yield-val { font-size: 1.75rem; color: #10b981; }
    .units-breakdown { display: flex; flex-direction: column; gap: 0.5rem; }
    .unit-item { display: flex; justify-content: space-between; padding: 0.25rem 0; border-bottom: 1px dashed #e2e8f0; }
    .order-pipeline { display: flex; flex-direction: column; gap: 0.75rem; }
    .pipeline-row { display: flex; justify-content: space-between; align-items: center; padding: 0.5rem; background: var(--erp-bg-secondary, #f8fafc); border-radius: 6px; }
    .mb-4 { margin-bottom: 1rem; }
    .mt-4 { margin-top: 1rem; }
  `]
})
export class ManufacturingAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(ManufacturingKpiApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly kpis = signal<ManufacturingKpisDto | null>(null);

  ngOnInit(): void {
    this.loadKpis();
  }

  loadKpis(): void {
    this.kpiApi.getKpis().subscribe({
      next: res => this.kpis.set(res.data),
      error: () => {}
    });
  }
}
