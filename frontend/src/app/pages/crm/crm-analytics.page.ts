import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { CrmKpiApi, CrmKpisDto } from '../../core/api/crm.api';
import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-crm-analytics-page',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    NzCardModule,
    NzGridModule,
    NzIconModule,
    NzButtonModule,
    NzProgressModule,
    NzTagModule,
    NzBadgeModule,
    StatCardComponent,
  ],
  template: `
    <h1 class="erp-page-title">CRM & Sales Pipeline Analytics</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadKpis()" [nzLoading]="loading()" data-testid="crm-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh KPIs
      </button>
    </div>

    <div *ngIf="kpis() as k">
      <!-- Top KPI Stat Grid -->
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="crm-stats">
        <erp-stat-card
          label="Total Inbound Leads"
          [value]="k.totalLeads"
          format="number"
          caption="total acquired"
          [loading]="loading()"
          [tone]="1" />

        <erp-stat-card
          label="Qualified Prospects"
          [value]="k.qualifiedLeads"
          format="number"
          caption="sales ready"
          [loading]="loading()"
          [tone]="4" />

        <erp-stat-card
          label="Open Deals"
          [value]="k.openOpportunitiesCount"
          format="number"
          caption="active in pipeline"
          [loading]="loading()"
          [tone]="3" />

        <erp-stat-card
          label="Open Pipeline Value"
          [value]="k.openPipelineValue"
          format="money"
          caption="weighted estimate"
          [loading]="loading()"
          [tone]="0" />

        <erp-stat-card
          label="Closed Won Deals"
          [value]="k.wonOpportunitiesCount"
          format="number"
          caption="contracts closed"
          [loading]="loading()"
          [tone]="2" />

        <erp-stat-card
          label="Total Won Revenue"
          [value]="k.wonOpportunitiesRevenue"
          format="money"
          caption="booked commercial revenue"
          [loading]="loading()"
          [tone]="0" />

        <erp-stat-card
          label="Lead Conversion Rate"
          [value]="k.leadConversionRatePercentage"
          format="percent"
          caption="prospect velocity"
          [loading]="loading()"
          [tone]="1" />

        <erp-stat-card
          label="Deal Win Rate"
          [value]="k.winRatePercentage"
          format="percent"
          caption="opportunity win probability"
          [loading]="loading()"
          [tone]="4" />
      </div>

      <!-- Performance Visual Breakdown -->
      <div nz-row [nzGutter]="[16, 16]" class="insights-row">
        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Lead-to-Opportunity Funnel Conversion" class="detail-card">
            <div class="funnel-metric">
              <div class="funnel-header">
                <span>Prospect Conversion Velocity</span>
                <strong class="text-primary">{{ k.leadConversionRatePercentage | number:'1.1-1' }}%</strong>
              </div>
              <nz-progress
                [nzPercent]="k.leadConversionRatePercentage"
                [nzStrokeColor]="{ '0%': '#10955d', '100%': '#2563eb' }">
              </nz-progress>
            </div>

            <div class="stats-summary-list">
              <div class="stat-line">
                <span class="label">Total Inbound Leads Acquired:</span>
                <span class="val">{{ k.totalLeads }}</span>
              </div>
              <div class="stat-line">
                <span class="label">Qualified for Sales Outreach:</span>
                <span class="val">{{ k.qualifiedLeads }}</span>
              </div>
            </div>
          </nz-card>
        </div>

        <div nz-col [nzXs]="24" [nzLg]="12">
          <nz-card nzTitle="Sales Deal Win Rate & Revenue Efficiency" class="detail-card">
            <div class="funnel-metric">
              <div class="funnel-header">
                <span>Opportunity Win Probability</span>
                <strong class="text-success">{{ k.winRatePercentage | number:'1.1-1' }}%</strong>
              </div>
              <nz-progress
                [nzPercent]="k.winRatePercentage"
                [nzStrokeColor]="{ '0%': '#10955d', '100%': '#059669' }">
              </nz-progress>
            </div>

            <div class="stats-summary-list">
              <div class="stat-line">
                <span class="label">Won Closed Contracts:</span>
                <span class="val text-success">{{ k.wonOpportunitiesCount }} deals</span>
              </div>
              <div class="stat-line">
                <span class="label">Active In-Flight Pipeline Value:</span>
                <span class="val text-primary">\${{ k.openPipelineValue | number:'1.0-0' }}</span>
              </div>
            </div>
          </nz-card>
        </div>
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
    .text-primary { color: #10955d; font-weight: 600; }
    .text-success { color: #10955d; font-weight: 600; }
    .funnel-metric { display: flex; flex-direction: column; gap: 8px; margin-bottom: 16px; }
    .funnel-header { display: flex; justify-content: space-between; font-size: 13px; color: #475569; }
    .stats-summary-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      border-top: 1px solid #f1f5f9;
      padding-top: 14px;
    }
    .stat-line {
      display: flex;
      justify-content: space-between;
      padding: 6px 0;
      border-bottom: 1px dashed #f1f5f9;
      font-size: 14px;
      color: #475569;
      &:last-child {
        border-bottom: none;
      }
    }
    .stat-line .val {
      font-weight: 600;
      color: #0f172a;
      font-variant-numeric: tabular-nums;
    }
  `]
})
export class CrmAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(CrmKpiApi);

  readonly kpis = signal<CrmKpisDto | null>(null);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadKpis();
  }

  loadKpis(): void {
    this.loading.set(true);
    this.kpiApi.getKpis().subscribe({
      next: res => {
        this.kpis.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

