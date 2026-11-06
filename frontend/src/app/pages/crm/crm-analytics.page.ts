import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { CrmKpiApi, CrmKpisDto, CrmOpportunitiesApi, PipelineStageSummaryDto } from '../../core/api/crm.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-crm-analytics-page',
  standalone: true,
  imports: [DecimalPipe, RouterModule, NzGridModule, NzIconModule, NzButtonModule, NzSkeletonModule, StatCardComponent, StatePanelComponent, StatusTagComponent],
  template: `
    <h1 class="erp-page-title">CRM & Sales Pipeline Analytics</h1>

    <div class="analytics-toolbar">
      <button nz-button (click)="loadKpis()" [nzLoading]="loading()" data-testid="crm-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The CRM figures could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadKpis()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="crm-stats">
        <erp-stat-card label="Total Leads" [value]="kpis()?.totalLeads ?? 0" format="number" caption="all time" [loading]="loading()" [tone]="1" />
        <erp-stat-card label="Qualified Leads" [value]="kpis()?.qualifiedLeads ?? 0" format="number" caption="qualified or converted" [loading]="loading()" [tone]="4" />
        <erp-stat-card label="Lead Conversion" [value]="kpis()?.leadConversionRatePercentage ?? 0" format="percent" caption="leads converted to deals" [loading]="loading()" [tone]="1" />
        <erp-stat-card label="Open Deals" [value]="kpis()?.openOpportunitiesCount ?? 0" format="number" caption="not yet won or lost" [loading]="loading()" [tone]="3" />
        <erp-stat-card label="Open Pipeline" [value]="kpis()?.openPipelineValue ?? 0" format="money" caption="expected value, not weighted" [loading]="loading()" [tone]="0" />
        <erp-stat-card label="Deals Won" [value]="kpis()?.wonOpportunitiesCount ?? 0" format="number" caption="closed won" [loading]="loading()" [tone]="2" />
        <erp-stat-card label="Won Revenue" [value]="kpis()?.wonOpportunitiesRevenue ?? 0" format="money" caption="final value of won deals" [loading]="loading()" [tone]="0" />
        <erp-stat-card label="Win Rate" [value]="kpis()?.winRatePercentage ?? 0" format="percent" caption="won out of all closed deals" [loading]="loading()" [tone]="4" />
      </div>

      <section class="panel" data-testid="crm-stage-breakdown">
        <div class="head">
          <h3>Deals by stage</h3>
          <a nz-button nzType="link" nzSize="small" routerLink="/crm/opportunities">Open pipeline</a>
        </div>
        @if (stages(); as rows) {
          @for (s of rows; track s.stage) {
            <div class="row">
              <span><erp-status [value]="s.stage" /></span>
              <div class="track" aria-hidden="true"><div class="fill" [style.width.%]="maxValue() ? (s.totalExpectedRevenue / maxValue()) * 100 : 0"></div></div>
              <span class="count">{{ s.count }}</span>
              <span class="value">{{ currency() }} {{ s.totalExpectedRevenue | number:'1.0-0' }}</span>
            </div>
          }
        } @else if (stagesFailed()) {
          <p class="muted">The stages could not be loaded.</p>
          <button nz-button nzSize="small" type="button" (click)="loadStages()">Try again</button>
        } @else {
          <nz-skeleton [nzActive]="true" [nzTitle]="false" [nzParagraph]="{ rows: 4 }" />
        }
      </section>
    }
  `,
  styles: [`
    .analytics-toolbar { display: flex; justify-content: flex-end; align-items: center; margin-bottom: 16px; }
    .panel { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); margin-top: 16px; padding: 16px 20px; border-radius: var(--erp-radius-card, 8px); }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 12px; }
    h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .row { display: grid; grid-template-columns: 130px 1fr 48px 150px; align-items: center; gap: 12px; padding: 6px 0; }
    .track { height: 6px; border-radius: 3px; background: var(--erp-color-bg-subtle); overflow: hidden; }
    .fill { height: 100%; border-radius: 3px; background: var(--erp-color-primary); }
    .count, .value { text-align: end; font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--erp-color-text-primary); }
    .value { font-weight: 600; }
    .muted { margin: 0 0 8px; color: var(--erp-color-text-muted); }
  `]
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
