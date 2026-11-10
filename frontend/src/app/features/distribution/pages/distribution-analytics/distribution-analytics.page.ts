import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { DistributionKpiApi, DistributionKpiSummaryDto } from '@core/api/distribution.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** Distribution analytics: the month's van sales, collections and route KPIs. */
@Component({
  selector: 'erp-distribution-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzCardModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent],
  templateUrl: './distribution-analytics.page.html',
  styleUrl: './distribution-analytics.page.scss',
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
