import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { ServicesKpiApi, ServicesKpisDto } from '@core/api/services.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { CanPipe } from '@shared/pipes/can.pipe';

/** Services analytics: active engagements, recurring retainer revenue, milestones ready to invoice and hours, for one legal entity. */
@Component({
  selector: 'erp-services-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzCardModule, NzGridModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent, CanPipe],
  templateUrl: './services-analytics.page.html',
  styleUrl: './services-analytics.page.scss',
})
export class ServicesAnalyticsPage implements OnInit {
  private readonly kpiApi = inject(ServicesKpiApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly kpi = signal<ServicesKpisDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);

  /** The summary is per legal entity: the one in use, else the first available. */
  private entityId: string | null = this.session.currentEntityId() || this.session.me()?.legalEntities[0]?.id || null;

  readonly milestonesTotal = computed(() => {
    const k = this.kpi();
    return k ? k.readyToInvoiceMilestonesValue + k.pendingMilestonesValue : 0;
  });
  readonly readyShare = computed(() => {
    const total = this.milestonesTotal();
    return total > 0 ? Math.round(((this.kpi()?.readyToInvoiceMilestonesValue ?? 0) / total) * 100) : 0;
  });
  readonly isEmpty = computed(() => {
    const k = this.kpi();
    return !!k && k.activeEngagements === 0 && k.retainerMonthlyRecurringRevenue === 0 && this.milestonesTotal() === 0 && k.totalLoggedHours === 0;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    if (this.entityId) {
      this.loadKpis(this.entityId);
      return;
    }
    this.legalEntitiesApi.list({ page: 1, pageSize: 1 }).subscribe({
      next: (res) => {
        this.entityId = res.items[0]?.id ?? null;
        if (this.entityId) this.loadKpis(this.entityId);
        else this.fail();
      },
      error: () => this.fail(),
    });
  }

  private loadKpis(entityId: string): void {
    this.kpiApi.getKpis(entityId).subscribe({
      next: (res) => { this.kpi.set(res); this.loading.set(false); },
      error: () => this.fail(),
    });
  }

  private fail(): void {
    this.failed.set(true);
    this.loading.set(false);
  }

  goToEngagements(): void {
    void this.router.navigateByUrl('/services/engagements');
  }
}
