import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { DistributionKpiApi, DistributionKpiSummaryDto } from '../../core/api/distribution.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';

@Component({
  selector: 'erp-distribution-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzCardModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent],
  template: `
    <div class="toolbar">
      <button nz-button (click)="load()" [nzLoading]="loading()" data-testid="distribution-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The distribution figures could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="distribution-analytics-stats">
        <erp-stat-card label="Routes" format="number" [value]="kpi()?.totalRoutes ?? null" [caption]="kpi() ? kpi()!.activeRoutes + ' active' : null" [loading]="loading()" link="/distribution/routes" [tone]="1" />
        <erp-stat-card label="Vans out today" format="number" [value]="kpi()?.activeTripsToday ?? null" caption="Trips in progress that left today" [loading]="loading()" link="/distribution/trips" [tone]="3" />
        <erp-stat-card label="Van sales this month" format="money" [value]="kpi()?.totalVanSalesMonth ?? null" [caption]="kpi() ? kpi()!.completedTripsMonth + (kpi()!.completedTripsMonth === 1 ? ' completed trip' : ' completed trips') : null" [loading]="loading()" tone="success" />
        <erp-stat-card label="Collected this month" format="money" [value]="kpi()?.totalCollectionsMonth ?? null" caption="From completed trips" [loading]="loading()" [tone]="2" />
        <erp-stat-card label="Wholesale price rules" format="number" [value]="kpi()?.wholesaleRulesCount ?? null" caption="Active rules" [loading]="loading()" link="/distribution/wholesale" [tone]="4" />
      </div>

      @if (kpi(); as k) {
        @if (k.totalRoutes === 0) {
          <erp-state-panel title="No routes yet" hint="Add a delivery route, then dispatch van trips on it to see sales and collections here." icon="compass" actionLabel="Go to routes" (action)="goTo('/distribution/routes')" />
        } @else if (k.completedTripsMonth === 0) {
          <erp-state-panel title="No completed trips this month" hint="Sales and collections appear here once a van trip is reconciled." icon="car" actionLabel="Go to van sales" (action)="goTo('/distribution/trips')" />
        } @else {
          <nz-card nzTitle="Collections against van sales this month" class="detail-card">
            <div class="metric-head">
              <span>{{ currency() }} {{ k.totalCollectionsMonth | number:'1.2-2' }} collected of {{ currency() }} {{ k.totalVanSalesMonth | number:'1.2-2' }} sold</span>
              <strong>{{ collectedRate() }}%</strong>
            </div>
            <nz-progress [nzPercent]="collectedRate()" [nzShowInfo]="false" nzStatus="normal"></nz-progress>
            @if (uncollected() > 0) {
              <p class="note">{{ currency() }} {{ uncollected() | number:'1.2-2' }} is still to be collected from these trips.</p>
            }
          </nz-card>
        }
      }
    }
  `,
  styles: [`
    .toolbar { display: flex; justify-content: flex-end; margin-block-end: 16px; }
    .detail-card { max-width: 640px; }
    .metric-head { display: flex; justify-content: space-between; gap: 12px; margin-block-end: 6px; color: var(--erp-color-text-muted); }
    .metric-head strong { color: var(--erp-color-text-primary); font-variant-numeric: tabular-nums; }
    .note { margin: 8px 0 0; color: var(--erp-color-text-muted); }
  `],
})
export class DistributionAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(DistributionKpiApi);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly kpi = signal<DistributionKpiSummaryDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

  readonly collectedRate = computed(() => {
    const k = this.kpi();
    return k && k.totalVanSalesMonth > 0 ? Math.min(100, Math.round((k.totalCollectionsMonth / k.totalVanSalesMonth) * 100)) : 0;
  });
  readonly uncollected = computed(() => {
    const k = this.kpi();
    return k ? k.totalVanSalesMonth - k.totalCollectionsMonth : 0;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.kpiApi.getSummary().subscribe({
      next: (res) => { this.kpi.set(res); this.loading.set(false); },
      error: () => { this.failed.set(true); this.loading.set(false); },
    });
  }

  goTo(url: string): void {
    void this.router.navigateByUrl(url);
  }
}
