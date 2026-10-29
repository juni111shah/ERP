import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { RetailKpiApi, RetailKpiSummaryDto } from '../../core/api/retail.api';

@Component({
  selector: 'erp-retail-analytics-page',
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
          <h2>Retail & Merchandising Analytics</h2>
          <p class="text-secondary">Network store distribution, active catalog tiers, and promotional campaigns</p>
        </div>
      </div>

      <div nz-row [nzGutter]="[16, 16]">
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.totalStores || 0"
              [nzTitle]="'Store Network'"
              [nzPrefix]="storeIcon">
            </nz-statistic>
            <ng-template #storeIcon><span nz-icon nzType="shop" class="icon-blue"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">{{ kpi()?.activeStores || 0 }} actively trading</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.activePriceLists || 0"
              [nzTitle]="'Active Price Lists'"
              [nzPrefix]="priceIcon">
            </nz-statistic>
            <ng-template #priceIcon><span nz-icon nzType="tag" class="icon-green"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">Standard & Wholesale tiers</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.activePromotions || 0"
              [nzTitle]="'Live Campaigns'"
              [nzPrefix]="promoIcon">
            </nz-statistic>
            <ng-template #promoIcon><span nz-icon nzType="gift" class="icon-orange"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">Discounts & coupons</span>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card [nzBordered]="false" class="kpi-card">
            <nz-statistic
              [nzValue]="kpi()?.promotionsRedeemedTotal || 0"
              [nzTitle]="'Total Redemptions'"
              [nzPrefix]="redeemIcon">
            </nz-statistic>
            <ng-template #redeemIcon><span nz-icon nzType="trophy" class="icon-purple"></span></ng-template>
            <div class="kpi-footer">
              <span class="text-secondary">Customer orders benefited</span>
            </div>
          </nz-card>
        </div>
      </div>

      <div nz-row [nzGutter]="[16, 16]" style="margin-top: 16px;">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Omnichannel Health & Compliance" [nzBordered]="false">
            <div class="health-item">
              <div class="health-header">
                <span>Active Store Outlets</span>
                <strong>{{ getStoreActiveRate() }}%</strong>
              </div>
              <nz-progress [nzPercent]="getStoreActiveRate()" nzStatus="active"></nz-progress>
            </div>

            <div class="health-item" style="margin-top: 20px;">
              <div class="health-header">
                <span>Promotion Engagement Index</span>
                <strong>88%</strong>
              </div>
              <nz-progress [nzPercent]="88" nzStrokeColor="#722ed1"></nz-progress>
            </div>

            <div class="health-item" style="margin-top: 20px;">
              <div class="health-header">
                <span>Price Consistency Across Tiers</span>
                <strong>99.4%</strong>
              </div>
              <nz-progress [nzPercent]="99" nzStrokeColor="#52c41a"></nz-progress>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Key Retail Merchandising Channels" [nzBordered]="false">
            <nz-table #channelTable [nzData]="channels" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Channel</th>
                  <th>Operating Mode</th>
                  <th nzAlign="right">Target Margin</th>
                  <th nzAlign="right">Status</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let ch of channelTable.data">
                  <td><strong>{{ ch.name }}</strong></td>
                  <td><nz-tag>{{ ch.type }}</nz-tag></td>
                  <td nzAlign="right" class="font-bold">{{ ch.margin }}</td>
                  <td nzAlign="right">
                    <nz-tag nzColor="success">Operational</nz-tag>
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
    .icon-green { color: #52c41a; font-size: 24px; }
    .icon-orange { color: #fa8c16; font-size: 24px; }
    .icon-purple { color: #722ed1; font-size: 24px; }
    .health-item {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .health-header {
      display: flex;
      justify-content: space-between;
      font-size: 14px;
    }
    .font-bold {
      font-weight: 600;
    }
  `]
})
export class RetailAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(RetailKpiApi);

  readonly kpi = signal<RetailKpiSummaryDto | null>(null);

  readonly channels = [
    { name: 'Flagship Showrooms', type: 'Direct Retail', margin: '42.5%' },
    { name: 'Shopping Mall Kiosks', type: 'Express Retail', margin: '48.0%' },
    { name: 'B2B Wholesale Accounts', type: 'Volume Contract', margin: '22.0%' },
    { name: 'Loyalty Club Portal', type: 'Member Exclusive', margin: '35.0%' }
  ];

  ngOnInit(): void {
    this.kpiApi.getSummary().subscribe({
      next: res => this.kpi.set(res)
    });
  }

  getStoreActiveRate(): number {
    const k = this.kpi();
    if (!k || k.totalStores === 0) return 100;
    return Math.round((k.activeStores / k.totalStores) * 100);
  }
}
