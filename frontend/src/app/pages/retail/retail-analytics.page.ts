import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { RetailKpiApi, RetailKpiSummaryDto } from '../../core/api/retail.api';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';

@Component({
  selector: 'erp-retail-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzCardModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent],
  template: `
    <div class="toolbar">
      <button nz-button (click)="load()" [nzLoading]="loading()" data-testid="retail-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The retail figures could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="retail-analytics-stats">
        <erp-stat-card label="Stores" format="number" [value]="kpi()?.totalStoresCount ?? null" [caption]="kpi() ? kpi()!.activeStoresCount + ' active' : null" [loading]="loading()" link="/retail/stores" [tone]="1" />
        <erp-stat-card label="Active price lists" format="number" [value]="kpi()?.activePriceListsCount ?? null" [loading]="loading()" link="/retail/pricelists" [tone]="0" />
        <erp-stat-card label="Active promotions" format="number" [value]="kpi()?.activePromotionsCount ?? null" caption="Switched on, whatever their dates" [loading]="loading()" link="/retail/promotions" [tone]="3" />
        <erp-stat-card label="Promotion redemptions" format="number" [value]="kpi()?.totalPromotionsRedeemed ?? null" caption="All time, across all promotions" [loading]="loading()" [tone]="4" />
      </div>

      @if (kpi(); as k) {
        @if (k.totalStoresCount === 0) {
          <erp-state-panel title="No stores yet" hint="Add your first store to start selling and tracking it here." icon="shop" actionLabel="Go to stores" (action)="goToStores()" />
        } @else {
          <nz-card nzTitle="Stores trading" class="detail-card">
            <div class="metric-head">
              <span>{{ k.activeStoresCount }} of {{ k.totalStoresCount }} stores are active</span>
              <strong>{{ activeRate() }}%</strong>
            </div>
            <nz-progress [nzPercent]="activeRate()" [nzShowInfo]="false" nzStatus="normal"></nz-progress>
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
  `],
})
export class RetailAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(RetailKpiApi);
  private readonly router = inject(Router);

  readonly kpi = signal<RetailKpiSummaryDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

  readonly activeRate = computed(() => {
    const k = this.kpi();
    return k && k.totalStoresCount > 0 ? Math.round((k.activeStoresCount / k.totalStoresCount) * 100) : 0;
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

  goToStores(): void {
    void this.router.navigateByUrl('/retail/stores');
  }
}
