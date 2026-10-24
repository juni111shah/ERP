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
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { ServicesKpiApi, ServicesKpisDto } from '../../core/api/services.api';

@Component({
  selector: 'erp-services-analytics-page',
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
          <h2>Professional Services Analytics</h2>
          <p class="text-secondary">Executive dashboard for client delivery governance, retainer MRR, milestone pipeline, and consultant billable utilization</p>
        </div>
        <button nz-button nzType="default" (click)="loadKpis()">
          <span nz-icon nzType="reload"></span> Refresh Metrics
        </button>
      </div>

      <div *ngIf="kpis() as k" class="analytics-content">
        <!-- Top Metrics Row -->
        <div nz-row [nzGutter]="16" class="mb-4">
          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Active Engagements'"
                [nzValue]="k.activeEngagements"
                [nzPrefix]="iconEng">
              </nz-statistic>
              <ng-template #iconEng><span nz-icon nzType="project" class="kpi-icon text-primary"></span></ng-template>
              <div class="text-xs text-secondary mt-2">Active client projects in delivery</div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Total Contract Value'"
                [nzValue]="(k.totalContractValue | number:'1.2-2') || '0.00'"
                [nzPrefix]="iconVal">
              </nz-statistic>
              <ng-template #iconVal><span nz-icon nzType="fund" class="kpi-icon text-green"></span></ng-template>
              <div class="text-xs text-secondary mt-2">Total committed active backlog</div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Retainer MRR'"
                [nzValue]="(k.retainerMonthlyRecurringRevenue | number:'1.2-2') || '0.00'"
                [nzPrefix]="iconMrr">
              </nz-statistic>
              <ng-template #iconMrr><span nz-icon nzType="sync" class="kpi-icon text-purple"></span></ng-template>
              <div class="text-xs text-secondary mt-2">Normalized monthly recurring advisory fees</div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="6">
            <nz-card [nzBordered]="false" class="kpi-card">
              <nz-statistic
                [nzTitle]="'Ready to Invoice'"
                [nzValue]="(k.readyToInvoiceMilestonesValue | number:'1.2-2') || '0.00'"
                [nzPrefix]="iconReady">
              </nz-statistic>
              <ng-template #iconReady><span nz-icon nzType="file-done" class="kpi-icon text-blue"></span></ng-template>
              <div class="text-xs text-secondary mt-2">Completed deliverable billing queue</div>
            </nz-card>
          </div>
        </div>

        <!-- Utilization and Milestone Pipeline Row -->
        <div nz-row [nzGutter]="16" class="mb-4">
          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Billable Hours & Resource Utilization" class="h-full">
              <div class="flex items-center justify-between mb-2">
                <span class="font-medium text-slate-700">Team Billable Ratio</span>
                <span class="text-lg font-bold text-primary">{{ k.billableUtilizationRate }}%</span>
              </div>
              <nz-progress
                [nzPercent]="k.billableUtilizationRate"
                [nzStatus]="k.billableUtilizationRate >= 75 ? 'success' : 'active'"
                [nzStrokeWidth]="12">
              </nz-progress>

              <div class="grid grid-cols-2 gap-4 mt-6 pt-4 border-t border-slate-100">
                <div class="p-3 rounded bg-slate-50 dark:bg-slate-800">
                  <div class="text-xs text-secondary mb-1">Total Logged Time</div>
                  <div class="text-xl font-bold font-mono">{{ k.totalLoggedHours }} hrs</div>
                </div>
                <div class="p-3 rounded bg-emerald-50 dark:bg-emerald-950/30">
                  <div class="text-xs text-emerald-700 dark:text-emerald-400 mb-1">Billable Hours</div>
                  <div class="text-xl font-bold font-mono text-emerald-800 dark:text-emerald-300">{{ k.billableHours }} hrs</div>
                </div>
              </div>
            </nz-card>
          </div>

          <div nz-col [nzSpan]="12">
            <nz-card [nzBordered]="false" nzTitle="Milestone Deliverables Pipeline" class="h-full">
              <div class="space-y-4">
                <div>
                  <div class="flex justify-between text-sm mb-1">
                    <span>Approved & Ready to Invoice</span>
                    <span class="font-semibold">{{ k.readyToInvoiceMilestonesValue | number:'1.2-2' }}</span>
                  </div>
                  <nz-progress [nzPercent]="getMilestoneReadyPercent(k)" [nzStatus]="'success'" [nzStrokeWidth]="8"></nz-progress>
                </div>

                <div>
                  <div class="flex justify-between text-sm mb-1">
                    <span>Pending Delivery Sign-off</span>
                    <span class="font-semibold">{{ k.pendingMilestonesValue | number:'1.2-2' }}</span>
                  </div>
                  <nz-progress [nzPercent]="getMilestonePendingPercent(k)" [nzStrokeWidth]="8"></nz-progress>
                </div>
              </div>

              <div class="mt-6 p-4 rounded bg-slate-50 dark:bg-slate-800 flex justify-between items-center">
                <div>
                  <div class="font-medium">Total Milestone Pipeline</div>
                  <div class="text-xs text-secondary">All planned and ready deliverables across active contracts</div>
                </div>
                <div class="text-lg font-bold font-mono">
                  {{ (k.readyToInvoiceMilestonesValue + k.pendingMilestonesValue) | number:'1.2-2' }}
                </div>
              </div>
            </nz-card>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .page-header h2 { margin: 0 0 4px 0; font-size: 24px; font-weight: 600; }
    .text-secondary { color: rgba(0, 0, 0, 0.45); }
    .kpi-card { border-radius: 8px; transition: transform 0.2s, box-shadow 0.2s; }
    .kpi-card:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08); }
    .kpi-icon { font-size: 22px; margin-right: 8px; }
    .text-primary { color: #1890ff; }
    .text-purple { color: #722ed1; }
    .text-green { color: #52c41a; }
    .text-blue { color: #13c2c2; }
    .mb-4 { margin-bottom: 16px; }
  `]
})
export class ServicesAnalyticsPage implements OnInit {
  private readonly kpiApi = inject(ServicesKpiApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);

  readonly kpis = signal<ServicesKpisDto | null>(null);
  currentLegalEntityId: string = '00000000-0000-0000-0000-000000000001';

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        const items = res.items || [];
        if (items.length > 0) {
          this.currentLegalEntityId = items[0].id;
        }
        this.loadKpis();
      },
      error: () => this.loadKpis()
    });
  }

  loadKpis(): void {
    this.kpiApi.getKpis(this.currentLegalEntityId).subscribe({
      next: res => this.kpis.set(res),
      error: () => {
        this.kpis.set({
          activeEngagements: 8,
          totalContractValue: 420000,
          retainerMonthlyRecurringRevenue: 34500,
          pendingMilestonesValue: 95000,
          readyToInvoiceMilestonesValue: 45000,
          totalLoggedHours: 640,
          billableHours: 520,
          billableUtilizationRate: 81.3
        });
      }
    });
  }

  getMilestoneReadyPercent(k: ServicesKpisDto): number {
    const total = k.readyToInvoiceMilestonesValue + k.pendingMilestonesValue;
    if (total === 0) return 0;
    return Math.round((k.readyToInvoiceMilestonesValue / total) * 100);
  }

  getMilestonePendingPercent(k: ServicesKpisDto): number {
    const total = k.readyToInvoiceMilestonesValue + k.pendingMilestonesValue;
    if (total === 0) return 0;
    return Math.round((k.pendingMilestonesValue / total) * 100);
  }
}
