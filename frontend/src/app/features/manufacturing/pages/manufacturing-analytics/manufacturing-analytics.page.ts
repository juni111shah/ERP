import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ManufacturingKpiApi, ManufacturingKpisDto } from '@core/api/manufacturing.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

@Component({
  selector: 'erp-manufacturing-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule, StatCardComponent, StatePanelComponent],
  templateUrl: './manufacturing-analytics.page.html',
  styleUrl: './manufacturing-analytics.page.scss',
})
export class ManufacturingAnalyticsPageComponent implements OnInit {
  private readonly kpiApi = inject(ManufacturingKpiApi);
  private readonly router = inject(Router);

  readonly kpi = signal<ManufacturingKpisDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

  /** The server sends 100% when nothing was produced, which is not a real yield. */
  readonly yieldRate = computed(() => {
    const k = this.kpi();
    return k && k.completedWorkOrdersMonth > 0 ? k.averageYieldRatePercentage : null;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.kpiApi.getKpis().subscribe({
      next: (res) => { this.kpi.set(res); this.loading.set(false); },
      error: () => { this.failed.set(true); this.loading.set(false); },
    });
  }

  goToBoms(): void {
    void this.router.navigateByUrl('/manufacturing/boms');
  }
}
