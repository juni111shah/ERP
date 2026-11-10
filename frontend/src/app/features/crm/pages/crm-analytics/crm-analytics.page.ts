import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { CrmKpiApi, CrmKpisDto, CrmOpportunitiesApi, PipelineStageSummaryDto } from '@core/api/crm.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

@Component({
  selector: 'erp-crm-analytics-page',
  standalone: true,
  imports: [DecimalPipe, RouterModule, NzGridModule, NzIconModule, NzButtonModule, NzSkeletonModule, StatCardComponent, StatePanelComponent, StatusTagComponent],
  templateUrl: './crm-analytics.page.html',
  styleUrl: './crm-analytics.page.scss'
})
export class CrmAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(CrmKpiApi);
  private readonly oppsApi = inject(CrmOpportunitiesApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly kpis = signal<CrmKpisDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly stages = signal<PipelineStageSummaryDto[] | null>(null);
  readonly stagesFailed = signal(false);
  readonly maxValue = computed(() => Math.max(0, ...(this.stages() ?? []).map((s) => s.totalExpectedRevenue)));

  ngOnInit(): void {
    this.loadKpis();
  }

  loadKpis(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.kpiApi.getKpis().subscribe({
      next: (res) => {
        this.kpis.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
    this.loadStages();
  }

  loadStages(): void {
    this.stagesFailed.set(false);
    this.oppsApi.getPipelineSummary().subscribe({
      next: (rows) => this.stages.set(rows),
      error: () => this.stagesFailed.set(true),
    });
  }
}
