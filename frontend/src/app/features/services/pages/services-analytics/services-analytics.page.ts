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

@Component({
  selector: 'erp-services-analytics-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NzButtonModule, NzCardModule, NzGridModule, NzIconModule, NzProgressModule, StatCardComponent, StatePanelComponent],
  template: `
    <div class="toolbar">
      <button nz-button (click)="load()" [nzLoading]="loading()" data-testid="services-refresh-kpis">
        <span nz-icon nzType="reload"></span>Refresh
      </button>
    </div>

    @if (failed()) {
      <erp-state-panel kind="error" title="The services figures could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="services-stats-grid">
        <erp-stat-card label="Active engagements" format="number" [value]="kpi()?.activeEngagements ?? null" [loading]="loading()" link="/services/engagements" [tone]="1" />
        <erp-stat-card label="Active contract value" format="money" [value]="kpi()?.totalContractValue ?? null" caption="Engagements in delivery" [loading]="loading()" tone="success" />
        <erp-stat-card label="Monthly retainer revenue" format="money" [value]="kpi()?.retainerMonthlyRecurringRevenue ?? null" caption="Active retainers, per month" [loading]="loading()" link="/services/retainers" [tone]="4" />
        <erp-stat-card label="Ready to invoice" format="money" [value]="kpi()?.readyToInvoiceMilestonesValue ?? null" caption="Milestones waiting for an invoice" [loading]="loading()" link="/services/milestones" tone="warning" />
      </div>

      @if (kpi(); as k) {
        @if (isEmpty()) {
          <erp-state-panel title="No services activity yet" hint="Create an engagement to start tracking milestones, retainers and hours." icon="solution" actionLabel="Go to engagements" (action)="goToEngagements()" />
        } @else {
          <div nz-row [nzGutter]="[16, 16]">
            <div nz-col [nzXs]="24" [nzLg]="12">
              <nz-card nzTitle="Billable hours" class="detail-card">
                @if (k.totalLoggedHours > 0) {
                  <div class="metric-head">
                    <span>{{ k.billableHours | number:'1.0-1' }} of {{ k.totalLoggedHours | number:'1.0-1' }} logged hours are billable</span>
                    <strong>{{ k.billableUtilizationRate | number:'1.0-1' }}%</strong>
                  </div>
                  <nz-progress [nzPercent]="k.billableUtilizationRate" [nzShowInfo]="false" nzStatus="normal"></nz-progress>
                  <div class="rows">
                    <div class="row"><span>Logged</span><span class="num">{{ k.totalLoggedHours | number:'1.0-1' }} h</span></div>
                    <div class="row"><span>Billable</span><span class="num">{{ k.billableHours | number:'1.0-1' }} h</span></div>
                    <div class="row"><span>Not billable</span><span class="num">{{ (k.totalLoggedHours - k.billableHours) | number:'1.0-1' }} h</span></div>
                  </div>
                } @else {
                  <p class="note">No hours have been logged yet.</p>
                }
              </nz-card>
            </div>
            <div nz-col [nzXs]="24" [nzLg]="12">
              <nz-card nzTitle="Milestones not yet invoiced" class="detail-card">
                @if (milestonesTotal() > 0) {
                  <div class="metric-head">
                    <span>Ready to invoice</span>
                    <strong>{{ currency() }} {{ k.readyToInvoiceMilestonesValue | number:'1.2-2' }}</strong>
                  </div>
                  <nz-progress [nzPercent]="readyShare()" [nzShowInfo]="false" nzStatus="normal"></nz-progress>
                  <div class="rows">
                    <div class="row"><span>Planned</span><span class="num">{{ currency() }} {{ k.pendingMilestonesValue | number:'1.2-2' }}</span></div>
                    <div class="row total"><span>Total</span><span class="num">{{ currency() }} {{ milestonesTotal() | number:'1.2-2' }}</span></div>
                  </div>
                } @else {
                  <p class="note">No milestones are waiting to be invoiced.</p>
                }
              </nz-card>
            </div>
          </div>
        }
      }
    }
  `,
  styles: [`
    .toolbar { display: flex; justify-content: flex-end; margin-block-end: 16px; }
    .detail-card { height: 100%; }
    .metric-head { display: flex; justify-content: space-between; gap: 12px; margin-block-end: 6px; color: var(--erp-color-text-muted); }
    .metric-head strong { color: var(--erp-color-text-primary); font-variant-numeric: tabular-nums; white-space: nowrap; }
    .rows { margin-block-start: 12px; border-block-start: 1px solid var(--erp-color-border-subtle); }
    .row { display: flex; justify-content: space-between; gap: 12px; padding-block: 8px; border-block-end: 1px solid var(--erp-color-border-subtle); }
    .row:last-child { border-block-end: 0; }
    .row.total { font-weight: 600; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .note { margin: 0; color: var(--erp-color-text-muted); }
  `],
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
