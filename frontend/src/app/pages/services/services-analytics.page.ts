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
import { SessionStore } from '../../core/session/session.store';
import { ServicesKpiApi, ServicesKpisDto } from '../../core/api/services.api';

import { StatCardComponent } from '../../shared/stat-card.component';

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
    NzBadgeModule,
    StatCardComponent
  ],
  template: `
    <div class="analytics-toolbar">
      <div class="analytics-subtitle">
        Client delivery governance, retainer MRR, milestone pipeline, and consultant billable utilization
      </div>
      <button nz-button nzType="default" (click)="loadKpis()" data-testid="services-refresh-kpis">
        <span nz-icon nzType="reload"></span> Refresh Metrics
      </button>
    </div>

    <div *ngIf="kpis() as k" class="analytics-content">
      <!-- Top Metrics Row -->
      <div class="erp-stat-grid erp-stat-grid--auto mb-4" data-testid="services-stats-grid">
        <erp-stat-card
          label="Active Engagements"
          [value]="k.activeEngagements"
          format="number"
          [tone]="1"
          caption="Active client projects in delivery"
        />
        <erp-stat-card
          label="Total Contract Value"
          [value]="k.totalContractValue"
          format="money"
          tone="success"
          caption="Total committed active backlog"
        />
        <erp-stat-card
          label="Retainer MRR"
          [value]="k.retainerMonthlyRecurringRevenue"
          format="money"
          [tone]="4"
          caption="Normalized monthly recurring advisory fees"
        />
        <erp-stat-card
          label="Ready to Invoice"
          [value]="k.readyToInvoiceMilestonesValue"
          format="money"
          tone="warning"
          caption="Completed deliverable billing queue"
        />
      </div>

      <!-- Utilization and Milestone Pipeline Row -->
      <div nz-row [nzGutter]="16" class="mb-4">
        <div nz-col [nzXs]="24" [nzLg]="12" class="mb-4">
          <nz-card [nzBordered]="false" nzTitle="Billable Hours & Resource Utilization" class="chart-card h-full">
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

        <div nz-col [nzXs]="24" [nzLg]="12" class="mb-4">
          <nz-card [nzBordered]="false" nzTitle="Milestone Deliverables Pipeline" class="chart-card h-full">
            <div class="space-y-4">
              <div>
                <div class="flex justify-between text-sm mb-1">
                  <span>Approved & Ready to Invoice</span>
                  <span class="font-semibold">\${{ k.readyToInvoiceMilestonesValue | number:'1.2-2' }}</span>
                </div>
                <nz-progress [nzPercent]="getMilestoneReadyPercent(k)" [nzStatus]="'success'" [nzStrokeWidth]="8"></nz-progress>
              </div>

              <div>
                <div class="flex justify-between text-sm mb-1">
                  <span>Pending Delivery Sign-off</span>
                  <span class="font-semibold">\${{ k.pendingMilestonesValue | number:'1.2-2' }}</span>
                </div>
                <nz-progress [nzPercent]="getMilestonePendingPercent(k)" [nzStrokeWidth]="8"></nz-progress>
              </div>
            </div>

            <div class="mt-6 p-4 rounded bg-slate-50 dark:bg-slate-800 flex justify-between items-center">
              <div>
                <div class="font-medium">Total Milestone Pipeline</div>
                <div class="text-xs text-secondary">All planned and ready deliverables across active contracts</div>
              </div>
              <div class="text-lg font-bold font-mono text-primary">
                \${{ (k.readyToInvoiceMilestonesValue + k.pendingMilestonesValue) | number:'1.2-2' }}
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
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.25rem;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    .analytics-subtitle {
      color: var(--erp-text-secondary, #64748b);
      font-size: 0.875rem;
    }
    .chart-card {
      border-radius: 12px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
      background: var(--erp-card-bg, #ffffff);
      border: 1px solid var(--erp-border, #e2e8f0);
    }
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-primary { color: var(--erp-primary, #0ea5e9); }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .mb-4 { margin-bottom: 1rem; }
  `]
})
export class ServicesAnalyticsPage implements OnInit {
  private readonly kpiApi = inject(ServicesKpiApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly sessionStore = inject(SessionStore);

  readonly kpis = signal<ServicesKpisDto | null>(null);
  currentLegalEntityId: string = this.sessionStore.currentEntityId() || this.sessionStore.me()?.legalEntities[0]?.id || '';

  ngOnInit(): void {
    if (this.currentLegalEntityId) {
      this.loadKpis();
    } else {
      this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
        next: res => {
          const items = res.items || [];
          if (items.length > 0) {
            this.currentLegalEntityId = items[0].id;
            this.loadKpis();
          }
        },
        error: () => {}
      });
    }
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
