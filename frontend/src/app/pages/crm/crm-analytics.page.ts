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
import { CrmKpiApi, CrmKpisDto } from '../../core/api/crm.api';

@Component({
  selector: 'erp-crm-analytics-page',
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
          <h2>CRM & Sales Pipeline Analytics</h2>
          <p class="text-secondary">Comprehensive overview of lead acquisition velocity, deal conversion rates, and closed won revenue</p>
        </div>
        <button nz-button nzType="default" (click)="loadKpis()">
          <span nz-icon nzType="reload"></span> Refresh KPIs
        </button>
      </div>

      <div *ngIf="kpis() as k" class="analytics-content">
        <!-- Top Lead Acquisition Row -->
        <div nz-row [nzGutter]="16" class="mb-4">
          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Total Inbound Leads'"
                [nzValue]="k.totalLeads"
                [nzPrefix]="iconLeads">
              </nz-statistic>
              <ng-template #iconLeads><span nz-icon nzType="user-add" class="kpi-icon text-primary"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Qualified Prospects'"
                [nzValue]="k.qualifiedLeads"
                [nzPrefix]="iconQual">
              </nz-statistic>
              <ng-template #iconQual><span nz-icon nzType="safety-certificate" class="kpi-icon text-purple"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Open Deals in Pipeline'"
                [nzValue]="k.openOpportunitiesCount"
                [nzPrefix]="iconOpps">
              </nz-statistic>
              <ng-template #iconOpps><span nz-icon nzType="funnel-plot" class="kpi-icon text-warning"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card highlight">
              <nz-statistic
                [nzTitle]="'Open Pipeline Value'"
                [nzValue]="(k.openPipelineValue | number:'1.0-0') || '0'"
                [nzPrefix]="'$'">
              </nz-statistic>
            </nz-card>
          </div>
        </div>

        <!-- Conversion and Revenue Row -->
        <div nz-row [nzGutter]="16" class="mb-4">
          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Closed Won Deals'"
                [nzValue]="k.wonOpportunitiesCount"
                [nzPrefix]="iconWon">
              </nz-statistic>
              <ng-template #iconWon><span nz-icon nzType="trophy" class="kpi-icon text-success"></span></ng-template>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card highlight-success">
              <nz-statistic
                [nzTitle]="'Total Won Revenue'"
                [nzValue]="(k.wonOpportunitiesRevenue | number:'1.0-0') || '0'"
                [nzPrefix]="'$'">
              </nz-statistic>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Lead Conversion Rate'"
                [nzValue]="(k.leadConversionRatePercentage | number:'1.1-1') || '0.0'"
                [nzSuffix]="'%'">
              </nz-statistic>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Deal Win Rate'"
                [nzValue]="(k.winRatePercentage | number:'1.1-1') || '0.0'"
                [nzSuffix]="'%'">
              </nz-statistic>
            </nz-card>
          </div>
        </div>

        <!-- Performance Visual Breakdown -->
        <div nz-row [nzGutter]="16">
          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Lead-to-Opportunity Funnel Conversion">
              <div class="funnel-metric">
                <div class="funnel-header">
                  <span>Prospect Conversion Velocity</span>
                  <strong class="text-primary">{{ k.leadConversionRatePercentage | number:'1.1-1' }}%</strong>
                </div>
                <nz-progress
                  [nzPercent]="k.leadConversionRatePercentage"
                  [nzStrokeColor]="{ '0%': '#3b82f6', '100%': '#8b5cf6' }">
                </nz-progress>
              </div>

              <div class="stats-summary-list mt-4">
                <div class="stat-line">
                  <span class="label">Total Inbound Leads Acquired:</span>
                  <span class="val font-medium">{{ k.totalLeads }}</span>
                </div>
                <div class="stat-line">
                  <span class="label">Qualified for Sales Outreach:</span>
                  <span class="val text-purple font-medium">{{ k.qualifiedLeads }}</span>
                </div>
              </div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Sales Deal Win Rate & Revenue Efficiency">
              <div class="funnel-metric">
                <div class="funnel-header">
                  <span>Opportunity Win Probability</span>
                  <strong class="text-success">{{ k.winRatePercentage | number:'1.1-1' }}%</strong>
                </div>
                <nz-progress
                  [nzPercent]="k.winRatePercentage"
                  [nzStrokeColor]="{ '0%': '#10b981', '100%': '#059669' }">
                </nz-progress>
              </div>

              <div class="stats-summary-list mt-4">
                <div class="stat-line">
                  <span class="label">Won Closed Contracts:</span>
                  <span class="val text-success font-medium">{{ k.wonOpportunitiesCount }} deals</span>
                </div>
                <div class="stat-line">
                  <span class="label">Active In-Flight Pipeline Value:</span>
                  <span class="val text-primary font-medium">\${{ k.openPipelineValue | number:'1.0-0' }}</span>
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
    .text-purple { color: #8b5cf6; }
    .text-warning { color: #f59e0b; }
    .text-success { color: #10b981; }
    .kpi-card { background: var(--erp-bg-secondary, #f8fafc); border-radius: 8px; }
    .kpi-card.highlight { border: 1px solid var(--erp-primary, #0ea5e9); background: rgba(14, 165, 233, 0.05); }
    .kpi-card.highlight-success { border: 1px solid #10b981; background: rgba(16, 185, 129, 0.05); }
    .kpi-icon { font-size: 1.25rem; margin-right: 0.5rem; }
    .funnel-metric { display: flex; flex-direction: column; gap: 0.5rem; }
    .funnel-header { display: flex; justify-content: space-between; font-size: 0.95rem; }
    .stats-summary-list { display: flex; flex-direction: column; gap: 0.5rem; }
    .stat-line { display: flex; justify-content: space-between; padding: 0.35rem 0; border-bottom: 1px dashed #e2e8f0; font-size: 0.9rem; }
    .mb-4 { margin-bottom: 1rem; }
    .mt-4 { margin-top: 1rem; }
  `]
})
export class CrmAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(CrmKpiApi);

  readonly kpis = signal<CrmKpisDto | null>(null);

  ngOnInit(): void {
    this.loadKpis();
  }

  loadKpis(): void {
    this.kpiApi.getKpis().subscribe({
      next: res => this.kpis.set(res),
      error: () => {}
    });
  }
}
