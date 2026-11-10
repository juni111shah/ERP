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
  template: `
    <div class="toolbar">
      <button nz-button (click)="load()" [nzLoading]="loading()" data-testid="manufacturing-analytics-refresh">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The manufacturing figures could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
      <div class="erp-stat-grid six" data-testid="manufacturing-analytics-stats">
        <erp-stat-card label="Active BOMs" format="number" [value]="kpi()?.activeBomsCount ?? null" [loading]="loading()" link="/manufacturing/boms" [tone]="1" />
        <erp-stat-card label="Open work orders" format="number" [value]="kpi()?.openWorkOrdersCount ?? null" caption="Planned or in progress" [loading]="loading()" link="/manufacturing/workorders" [tone]="3" />
        <erp-stat-card label="Completed this month" format="number" [value]="kpi()?.completedWorkOrdersMonth ?? null" caption="Work orders" [loading]="loading()" tone="success" />
        <erp-stat-card label="Produced this month" format="number" [value]="kpi()?.totalProducedQuantityMonth ?? null" caption="Good units, all products" [loading]="loading()" [tone]="2" />
        <erp-stat-card label="Yield this month" format="percent" [value]="yieldRate()" [caption]="yieldRate() === null ? 'Nothing completed yet' : 'Good units against good plus scrapped'" [loading]="loading()" [tone]="4" />
        <erp-stat-card label="Production cost this month" format="money" [value]="kpi()?.totalManufacturingCostMonth ?? null" caption="Actual cost of completed orders" [loading]="loading()" [tone]="0" />
      </div>

      @if (kpi(); as k) {
        @if (k.activeBomsCount === 0 && k.openWorkOrdersCount === 0 && k.completedWorkOrdersMonth === 0) {
          <erp-state-panel title="No production yet" hint="Add a bill of materials, then schedule a work order to see figures here." icon="tool" actionLabel="Go to bills of materials" (action)="goToBoms()" />
        }
      }
    }
  `,
  styles: [`
    .toolbar { display: flex; justify-content: flex-end; margin-block-end: 16px; }
    .erp-stat-grid.six { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
  `],
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
