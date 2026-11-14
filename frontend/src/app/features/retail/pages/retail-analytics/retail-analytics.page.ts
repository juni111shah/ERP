import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { RetailKpiApi, RetailKpiSummaryDto } from '@core/api/retail.api';
import { SessionStore } from '@core/session/session.store';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** Retail at a glance: store, price list and promotion counts, with a link to the stores. */
@Component({
  selector: 'erp-retail-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzCardModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent],
  templateUrl: './retail-analytics.page.html',
  styleUrl: './retail-analytics.page.scss',
})
export class RetailAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(RetailKpiApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);

  /** Each card links to its list only for someone who may open that list. */
  readonly storesLink = computed(() => (this.session.hasPermission('retail.stores.read') ? '/retail/stores' : null));
  readonly priceListsLink = computed(() => (this.session.hasPermission('retail.pricelists.read') ? '/retail/pricelists' : null));
  readonly promotionsLink = computed(() => (this.session.hasPermission('retail.promotions.read') ? '/retail/promotions' : null));

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
    if (!this.storesLink()) return;
    void this.router.navigateByUrl('/retail/stores');
  }
}
