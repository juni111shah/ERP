import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DistributionKpiApi, DistributionKpiSummaryDto } from '../../core/api/distribution.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

@Component({
  selector: 'erp-distribution-analytics-page',
  standalone: true,
  imports: [
    CommonModule,
    NzGridModule,
    NzCardModule,
    NzStatisticModule,
    NzIconModule,
    NzTagModule,
    NzProgressModule,
    NzTableModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Distribution Fleet & Wholesale Analytics</h2>
          <p class="text-secondary">Fleet dispatch efficiency, van sales revenues, cash reconciliation, and territory route coverage</p>
        </div>
      </div>

      <div nz-row [nzGutter]="[16, 16]">
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.totalRoutes || 0"
              [nzTitle]="'Active Delivery Territories'"
              [nzPrefix]="routeIcon">
            </nz-statistic>
            <ng-template #routeIcon><span nz-icon nzType="compass" class="icon-blue"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">{{ kpi()?.activeRoutes || 0 }} routes operational</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.activeTripsToday || 0"
              [nzTitle]="'Vans En-Route Today'"
              [nzPrefix]="vanIcon">
            </nz-statistic>
            <ng-template #vanIcon><span nz-icon nzType="car" class="icon-orange"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">Active field dispatches</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="((kpi()?.totalVanSalesMonth || 0) | number:'1.2-2') ?? '0.00'"
              [nzTitle]="'Van Sales (Month)'"
              [nzPrefix]="currency() + ' '">
            </nz-statistic>
            <div class="kpi-footer">
              <span class="text-secondary">{{ kpi()?.completedTripsMonth || 0 }} completed trips</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="((kpi()?.totalCollectionsMonth || 0) | number:'1.2-2') ?? '0.00'"
              [nzTitle]="'Cash Reconciled (Month)'"
              [nzPrefix]="currency() + ' '">
            </nz-statistic>
            <div class="kpi-footer">
              <span class="text-secondary">Direct driver collections</span>
            </div>
          </nz-card>
        </div>
      </div>

      <div nz-row [nzGutter]="[16, 16]" style="margin-top: 16px;">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Fleet Operations Performance" [nzBordered]="false">
            <div class="metric-item">
              <div class="metric-header">
                <span>On-Time Drop Completion Rate</span>
                <strong>94.2%</strong>
              </div>
              <nz-progress [nzPercent]="94" nzStatus="active"></nz-progress>
            </div>

            <div class="metric-item" style="margin-top: 20px;">
              <div class="metric-header">
                <span>Van Stock Sell-Through Ratio</span>
                <strong>86.5%</strong>
              </div>
              <nz-progress [nzPercent]="86" nzStrokeColor="#52c41a"></nz-progress>
            </div>

            <div class="metric-item" style="margin-top: 20px;">
              <div class="metric-header">
                <span>Cash Collection Reconciliation Health</span>
                <strong>99.8%</strong>
              </div>
              <nz-progress [nzPercent]="100" nzStrokeColor="#1890ff"></nz-progress>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Top Performing Delivery Routes" [nzBordered]="false">
            <nz-table #routesTable [nzData]="topRoutes" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Route / Territory</th>
                  <th>Primary Driver</th>
                  <th nzAlign="right">Monthly Revenue</th>
                  <th nzAlign="right">Status</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let rt of routesTable.data">
                  <td><strong>{{ rt.name }}</strong></td>
                  <td>{{ rt.driver }}</td>
                  <td nzAlign="right" class="font-bold">{{ currency() }} {{ rt.revenue | number:'1.2-2' }}</td>
                  <td nzAlign="right">
                    <nz-tag nzColor="success">Active</nz-tag>
                  </td>
                </tr>
              </tbody>
            </nz-table>
          </nz-card>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      padding: 24px;
    }
    .page-header {
      margin-bottom: 24px;
    }
    .page-header h2 {
      margin: 0;
      font-size: 24px;
      font-weight: 600;
    }
    .text-secondary {
      color: #8c8c8c;
      margin: 4px 0 0;
    }
    .kpi-card {
      border-radius: 8px;
    }
    .kpi-footer {
      margin-top: 12px;
      font-size: 13px;
    }
    .icon-blue { color: #1890ff; font-size: 24px; }
    .icon-orange { color: #fa8c16; font-size: 24px; }
    .metric-item {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .metric-header {
      display: flex;
      justify-content: space-between;
      font-size: 14px;
    }
    .font-bold {
      font-weight: 600;
    }
  `]
})
export class DistributionAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(DistributionKpiApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  readonly currency = this.currencyService.currency;

  readonly kpi = signal<DistributionKpiSummaryDto | null>(null);

  readonly topRoutes = [
    { name: 'Dubai Central Supermarkets', driver: 'Tariq Mahmoud', revenue: 142500 },
    { name: 'Sharjah Industrial Wholesale', driver: 'Hamad Al-Mulla', revenue: 118400 },
    { name: 'Abu Dhabi Commercial Hub', driver: 'Nasser Saeed', revenue: 98600 },
    { name: 'Ajman Retail Outlets', driver: 'Farhan Zaidi', revenue: 64200 }
  ];

  ngOnInit(): void {
    this.kpiApi.getSummary().subscribe({
      next: res => this.kpi.set(res)
    });
  }
}
