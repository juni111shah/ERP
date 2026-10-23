import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DashboardAlert, DashboardApi, DashboardChart, DashboardDto, KpiCard } from '../core/api/admin.api';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { SessionStore } from '../core/session/session.store';
import { SimpleChartComponent } from '../shared/simple-chart.component';
import { StatCardComponent, StatFormat } from '../shared/stat-card.component';

const URGENCY: Record<string, { label: string; color: string }> = {
  action: { label: 'Action needed', color: 'red' },
  upcoming: { label: 'Upcoming', color: 'gold' },
  info: { label: 'For information', color: 'blue' },
};

type DomainCategory = 'all' | 'finance' | 'real-estate' | 'hr' | 'inventory';

@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    RouterLink,
    SimpleChartComponent,
    StatCardComponent,
    NzIconModule,
    NzSkeletonModule,
    NzSpinModule,
    NzTagModule,
  ],
  styles: [`
    :host {
      display: block;
      color: var(--erp-color-text-primary, #1e293b);
      background: var(--erp-color-bg-canvas, #f8fafc);
      padding-bottom: 32px;
    }

    .dashboard-container {
      max-width: 1600px;
      margin: 0 auto;
    }

    /* ---- HERO HEADER ---- */
    .hero {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
      border-radius: 14px;
      padding: 18px 24px;
      margin-bottom: 16px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
    }
    .hero-content {
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .hero .eyebrow {
      letter-spacing: 0.08em;
      font-size: 11px;
      font-weight: 600;
      color: var(--erp-color-primary, #0b8457);
      text-transform: uppercase;
    }
    .hero h1 {
      color: var(--erp-color-text-primary, #0f172a);
      margin: 2px 0;
      font-size: 24px;
      font-weight: 700;
      letter-spacing: -0.02em;
    }
    .hero p {
      margin: 0;
      font-size: 13px;
      color: var(--erp-color-text-secondary, #64748b);
    }
    .hero-right {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .scope-pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 6px 14px;
      background: #f8fafc;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      border-radius: 20px;
      font-size: 12px;
      font-weight: 500;
      color: var(--erp-color-text-secondary, #475569);
    }
    .pulse-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: #10b981;
      display: inline-block;
      box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.25);
    }

    /* ---- MODULE FILTER SEGMENTED TABS ---- */
    .filter-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
      margin-bottom: 20px;
      background: #ffffff;
      padding: 6px 8px;
      border-radius: 12px;
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
    }
    .category-tabs {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }
    .tab-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 6px 14px;
      font-size: 13px;
      font-weight: 500;
      border-radius: 8px;
      border: none;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .tab-pill:hover {
      background: #f1f5f9;
      color: var(--erp-color-text-primary, #0f172a);
    }
    .tab-pill.active {
      background: #e8f7f0;
      color: #0b8457;
      font-weight: 600;
    }
    .tab-count {
      display: inline-block;
      padding: 1px 6px;
      border-radius: 10px;
      font-size: 11px;
      font-weight: 600;
      background: #edf2f7;
      color: #64748b;
    }
    .tab-pill.active .tab-count {
      background: #cbeedd;
      color: #08704b;
    }
    .view-mode-toggle {
      display: inline-flex;
      background: #f1f5f9;
      padding: 3px;
      border-radius: 8px;
      gap: 2px;
    }
    .toggle-btn {
      padding: 4px 10px;
      font-size: 12px;
      font-weight: 500;
      border: none;
      background: transparent;
      border-radius: 6px;
      cursor: pointer;
      color: #64748b;
      transition: all 0.2s;
    }
    .toggle-btn.active {
      background: #ffffff;
      color: #0f172a;
      font-weight: 600;
      box-shadow: 0 1px 2px rgba(0,0,0,0.06);
    }

    /* ---- KPI SECTIONS ---- */
    .kpi-section {
      margin-bottom: 22px;
    }
    .section-eyebrow {
      font-size: 12px;
      font-weight: 600;
      color: var(--erp-color-text-secondary, #64748b);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin: 0 0 10px 2px;
      display: flex;
      align-items: center;
      gap: 6px;
    }

    /* ---- MAIN DASHBOARD SPLIT (SINGLE TABBED ANALYTICS HUB + ALERTS) ---- */
    .layout {
      display: grid;
      grid-template-columns: 1fr 380px;
      gap: 20px;
      align-items: stretch;
    }
    @media (max-width: 1100px) {
      .layout {
        grid-template-columns: 1fr;
      }
    }

    /* Analytics Hub */
    .analytics-hub {
      display: flex;
      flex-direction: column;
      height: 100%;
    }

    .chart-panel {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
      border-radius: 14px;
      padding: 20px 22px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    .chart-panel-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 8px;
      flex-wrap: wrap;
      gap: 12px;
    }
    .chart-titles {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .chart-panel h3 {
      margin: 0;
      font-size: 17px;
      font-weight: 700;
      color: var(--erp-color-text-primary, #0f172a);
    }
    .chart-panel .sub {
      font-size: 12px;
      color: var(--erp-color-text-secondary, #64748b);
      margin: 0;
    }

    .chart-selector-tabs {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }
    .chart-tab-btn {
      padding: 5px 12px;
      font-size: 12px;
      font-weight: 500;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
      background: #ffffff;
      color: #64748b;
      cursor: pointer;
      transition: all 0.2s;
    }
    .chart-tab-btn.active {
      background: #eff6ff;
      border-color: #93c5fd;
      color: #2563eb;
      font-weight: 600;
    }
    .chart-tab-btn:hover:not(.active) {
      background: #f8fafc;
      border-color: #cbd5e1;
    }

    .chart-canvas-wrap {
      flex: 1;
      display: flex;
      align-items: center;
      margin-top: 14px;
    }

    /* Alerts Panel */
    .alerts-panel {
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
      border-radius: 14px;
      padding: 20px 22px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    .alert-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 14px;
    }
    .alert-header h3 {
      margin: 0;
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
    }
    .alert-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      overflow-y: auto;
      max-height: 400px;
    }
    .alert {
      display: flex;
      gap: 12px;
      padding: 10px 12px;
      border-radius: 10px;
      border: 1px solid transparent;
      color: inherit;
      text-decoration: none;
      align-items: flex-start;
      transition: background 0.15s, border-color 0.15s;
    }
    .alert:hover {
      background: #f8fafc;
      border-color: #e2e8f0;
    }
    .alert-icon-wrap {
      width: 34px;
      height: 34px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      font-size: 15px;
    }
    .icon-critical {
      background: #fee2e2;
      color: #ef4444;
    }
    .icon-warning {
      background: #fef3c7;
      color: #f59e0b;
    }
    .icon-info {
      background: #e0f2fe;
      color: #0284c7;
    }
    .alert .msg {
      flex: 1;
      font-size: 13px;
      line-height: 1.4;
      color: var(--erp-color-text-primary, #1e293b);
    }
    .alert-tags {
      display: flex;
      gap: 6px;
      margin-bottom: 4px;
      flex-wrap: wrap;
    }
    .empty-state {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 24px 0;
      color: var(--erp-color-text-secondary, #64748b);
      font-size: 13px;
    }
  `],
  template: `
    <div class="dashboard-container">
      <!-- Hero Banner -->
      <div class="hero">
        <div class="hero-content">
          <div class="eyebrow">{{ session.me()?.workspace?.name }} · {{ 'dashboard.overview' | translate: 'Overview' }}</div>
          <h1 data-testid="dashboard-title">{{ 'dashboard.welcome' | translate: 'Welcome' }}, {{ session.me()?.user?.fullName }}</h1>
          <p>{{ 'ui.dash.builtFrom' | translate: 'Every figure is built from posted transactions' }} · {{ 'ui.common.asOfLc' | translate: 'as of' }} {{ data()?.asOf }}.</p>
        </div>
        <div class="hero-right">
          <div class="scope-pill">
            <span class="pulse-dot"></span>
            <span>{{ session.currentEntityId() ? ('Entity: ' + (currentEntityName() || 'Active')) : timezoneLabel() }}</span>
          </div>
        </div>
      </div>

      <nz-spin [nzSpinning]="loading() && !!data()">
        @if (!data()) {
          <!-- First load: skeletons instead of blank panels or zeros; real values replace them as soon as the data arrives -->
          <div class="kpi-section" data-testid="dashboard-loading">
            <div class="erp-stat-grid">
              @for (i of [0, 1, 2, 3]; track i) { <erp-stat-card label="" [loading]="true" [tone]="i" /> }
            </div>
            <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }"></nz-skeleton>
          </div>
        }
        @if (data(); as d) {
          <!-- Domain Filter Navigation & View Mode Switcher -->
          <div class="filter-bar">
            <div class="category-tabs">
              <button
                class="tab-pill"
                [class.active]="activeCategory() === 'all'"
                (click)="setCategory('all')">
                <span nz-icon nzType="appstore"></span>
                <span>All Overview</span>
                <span class="tab-count">{{ d.kpis.length }}</span>
              </button>

              @if (hasCategory('finance')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'finance'"
                  (click)="setCategory('finance')">
                  <span nz-icon nzType="dollar"></span>
                  <span>Finance</span>
                  <span class="tab-count">{{ getCategoryKpis('finance').length }}</span>
                </button>
              }

              @if (hasCategory('real-estate')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'real-estate'"
                  (click)="setCategory('real-estate')">
                  <span nz-icon nzType="home"></span>
                  <span>Real Estate</span>
                  <span class="tab-count">{{ getCategoryKpis('real-estate').length }}</span>
                </button>
              }

              @if (hasCategory('hr')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'hr'"
                  (click)="setCategory('hr')">
                  <span nz-icon nzType="team"></span>
                  <span>HR & People</span>
                  <span class="tab-count">{{ getCategoryKpis('hr').length }}</span>
                </button>
              }

              @if (hasCategory('inventory')) {
                <button
                  class="tab-pill"
                  [class.active]="activeCategory() === 'inventory'"
                  (click)="setCategory('inventory')">
                  <span nz-icon nzType="shopping"></span>
                  <span>Inventory</span>
                  <span class="tab-count">{{ getCategoryKpis('inventory').length }}</span>
                </button>
              }
            </div>

            <div class="view-mode-toggle">
              <button
                class="toggle-btn"
                [class.active]="viewMode() === 'structured'"
                (click)="viewMode.set('structured')">
                Curated
              </button>
              <button
                class="toggle-btn"
                [class.active]="viewMode() === 'all'"
                (click)="viewMode.set('all')">
                All Cards
              </button>
            </div>
          </div>

          <!-- KPI Cards Section -->
          <div class="kpi-section" data-testid="kpi-grid">
            @if (activeCategory() === 'all' && viewMode() === 'structured') {
              <!-- 1. Executive Highlights (Primary 4 Cards across 4 columns) -->
              <div class="section-eyebrow">
                <span nz-icon nzType="fund"></span> Executive Highlights
              </div>
              <div class="erp-stat-grid">
                @for (k of heroKpis(); track k.key; let i = $index) {
                  <erp-stat-card [label]="k.label" [value]="k.value" [format]="statFormat(k)" [caption]="k.caption ?? null" [tone]="i" [link]="k.link ?? null" [testId]="'kpi-' + k.key" />
                }
              </div>

              <!-- 2. Operational Metrics (Exactly 4 columns = 3 full rows of 4 cards, perfectly balanced) -->
              @if (secondaryKpis().length > 0) {
                <div class="section-eyebrow" style="margin-top: 16px;">
                  <span nz-icon nzType="dashboard"></span> {{ 'ui.dash.opMetrics' | translate: 'Operational Metrics' }} ({{ secondaryKpis().length }})
                </div>
                <div class="erp-stat-grid erp-stat-grid--compact">
                  @for (k of secondaryKpis(); track k.key) {
                    <erp-stat-card [compact]="true" [label]="k.label" [value]="k.value" [format]="statFormat(k)" [caption]="k.caption ?? null" [link]="k.link ?? null" [testId]="'kpi-' + k.key" />
                  }
                </div>
              }
            } @else {
              <!-- Full / Category Grid View -->
              <div class="erp-stat-grid erp-stat-grid--full">
                @for (k of visibleKpis(); track k.key; let i = $index) {
                  <erp-stat-card [label]="k.label" [value]="k.value" [format]="statFormat(k)" [caption]="k.caption ?? null" [tone]="i" [link]="k.link ?? null" [testId]="'kpi-' + k.key" />
                }
              </div>
            }
          </div>

          <!-- Main Layout: Single Tabbed Analytics Hub (Left) & Alerts (Right) -->
          <div class="layout">
            <!-- Left Side: Single Interactive Analytics Hub -->
            <div class="analytics-hub">
              @if (visibleCharts().length > 0) {
                <div class="chart-panel" [attr.data-testid]="'chart-' + currentChart()?.key">
                  <div class="chart-panel-header">
                    <div class="chart-titles">
                      <h3>{{ currentChart()?.title }}</h3>
                      @if (currentChart()?.subtitle) {
                        <div class="sub">{{ currentChart()?.subtitle }}</div>
                      }
                    </div>
                    <div class="chart-selector-tabs">
                      @for (c of visibleCharts(); track c.key) {
                        <button
                          class="chart-tab-btn"
                          [class.active]="selectedChartKey() === c.key"
                          (click)="selectedChartKey.set(c.key)">
                          {{ c.title }}
                        </button>
                      }
                    </div>
                  </div>

                  <div class="chart-canvas-wrap">
                    @if (currentChart(); as c) {
                      <erp-simple-chart [chart]="c" />
                    }
                  </div>
                </div>
              }
            </div>

            <!-- Right Side: Alerts & To-Dos -->
            <div class="alerts-panel" data-testid="alerts">
              <div class="alert-header">
                <h3>{{ 'dashboard.alerts' | translate: 'Alerts and to-dos' }}</h3>
                <span class="tab-count">{{ d.alerts.length }}</span>
              </div>
              <div class="alert-list">
                @for (a of d.alerts; track $index) {
                  <a class="alert" [routerLink]="a.link ?? null">
                    <div class="alert-icon-wrap" [class]="'icon-' + a.severity">
                      <span nz-icon [nzType]="a.severity === 'info' ? 'bell' : (a.severity === 'critical' ? 'warning' : 'bulb')"></span>
                    </div>
                    <div class="msg">
                      <div class="alert-tags">
                        <nz-tag>{{ a.category }}</nz-tag>
                        <nz-tag [nzColor]="urgency(a.urgency).color">{{ urgency(a.urgency).label }}</nz-tag>
                      </div>
                      <div>{{ a.message }}</div>
                    </div>
                  </a>
                } @empty {
                  <div class="empty-state">
                    <span nz-icon nzType="check-circle" style="color: #10b981; font-size: 18px;"></span>
                    <span>Nothing needs your attention.</span>
                  </div>
                }
              </div>
            </div>
          </div>
        }
      </nz-spin>
    </div>
  `,
})
export class DashboardPage {
  readonly session = inject(SessionStore);
  private readonly api = inject(DashboardApi);

  readonly data = signal<DashboardDto | null>(null);
  readonly loading = signal(false);
  readonly activeCategory = signal<DomainCategory>('all');
  readonly viewMode = signal<'structured' | 'all'>('structured');
  readonly selectedChartKey = signal<string | null>(null);

  readonly currentEntityName = computed(() => {
    const id = this.session.currentEntityId();
    if (!id) return null;
    return this.session.me()?.legalEntities?.find((e) => e.id === id)?.name ?? null;
  });

  readonly timezoneLabel = computed(() => {
    try {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
      const offsetMin = -new Date().getTimezoneOffset();
      const sign = offsetMin >= 0 ? '+' : '-';
      const hours = String(Math.floor(Math.abs(offsetMin) / 60)).padStart(2, '0');
      const mins = String(Math.abs(offsetMin) % 60).padStart(2, '0');
      return `GMT${sign}${hours}:${mins} (${tz.replace('_', ' ')})`;
    } catch {
      return 'GMT+03:00 (Standard Time)';
    }
  });

  constructor() {
    effect(() => {
      this.session.currentEntityId();
      untracked(() => this.load());
    });
  }

  setCategory(cat: DomainCategory): void {
    this.activeCategory.set(cat);
    // Reset selected chart to the first in the newly selected category
    const charts = this.getCategoryCharts(cat);
    if (charts.length > 0) {
      this.selectedChartKey.set(charts[0].key);
    }
  }

  urgency(u: string): { label: string; color: string } {
    return URGENCY[u] ?? URGENCY['info'];
  }

  statFormat(k: KpiCard): StatFormat { return k.format === 'percent' ? 'percent' : k.format === 'number' ? 'number' : 'money'; }

  /* ---- DOMAIN CLASSIFIERS ---- */
  private classifyKpi(key: string): DomainCategory {
    if (['portfolio', 'occupancy', 'contracted-rent', 'expiring', 'work-orders'].includes(key) || key.startsWith('re-')) return 'real-estate';
    if (['employees', 'payroll-runs', 'payroll-open', 'reviews', 'reviews-pending'].includes(key) || key.startsWith('hr-')) return 'hr';
    if (['stock-value', 'inventory-value'].includes(key) || key.startsWith('inv-')) return 'inventory';
    return 'finance';
  }

  private classifyChart(key: string): DomainCategory {
    if (['occupancy', 'lease-expiries'].includes(key) || key.startsWith('re-')) return 'real-estate';
    return 'finance';
  }

  hasCategory(cat: DomainCategory): boolean {
    const kpis = this.data()?.kpis ?? [];
    return kpis.some((k) => this.classifyKpi(k.key) === cat);
  }

  getCategoryKpis(cat: DomainCategory): KpiCard[] {
    const kpis = this.data()?.kpis ?? [];
    if (cat === 'all') return kpis;
    return kpis.filter((k) => this.classifyKpi(k.key) === cat);
  }

  getCategoryCharts(cat: DomainCategory): DashboardChart[] {
    const charts = this.data()?.charts ?? [];
    if (cat === 'all') return charts;
    return charts.filter((c) => this.classifyChart(c.key) === cat);
  }

  readonly visibleKpis = computed<KpiCard[]>(() => {
    return this.getCategoryKpis(this.activeCategory());
  });

  /** The top 4 executive highlight cards for the overview */
  readonly heroKpis = computed<KpiCard[]>(() => {
    const all = this.data()?.kpis ?? [];
    if (all.length <= 4) return all;

    const priorityKeys = ['portfolio', 'occupancy', 'receivables', 'net-result', 'cash', 'collected'];
    const chosen: KpiCard[] = [];

    for (const pk of priorityKeys) {
      const match = all.find((k) => k.key === pk);
      if (match && !chosen.includes(match)) {
        chosen.push(match);
      }
      if (chosen.length === 4) break;
    }

    for (const k of all) {
      if (chosen.length >= 4) break;
      if (!chosen.includes(k)) chosen.push(k);
    }
    return chosen;
  });

  /** The remaining 12 secondary cards in the overview */
  readonly secondaryKpis = computed<KpiCard[]>(() => {
    const all = this.data()?.kpis ?? [];
    const heroes = this.heroKpis();
    return all.filter((k) => !heroes.some((h) => h.key === k.key));
  });

  readonly visibleCharts = computed<DashboardChart[]>(() => {
    return this.getCategoryCharts(this.activeCategory());
  });

  readonly currentChart = computed<DashboardChart | null>(() => {
    const charts = this.visibleCharts();
    if (charts.length === 0) return null;
    const selectedKey = this.selectedChartKey();
    if (selectedKey) {
      const match = charts.find((c) => c.key === selectedKey);
      if (match) return match;
    }
    return charts[0];
  });

  private pending?: Subscription;

  private load(): void {
    this.loading.set(true);
    this.pending?.unsubscribe(); // a slow answer for the previous entity must not overwrite the current one
    this.pending = this.api.get().subscribe({
      next: (d) => {
        this.data.set(d);
        if (d.charts.length > 0 && !this.selectedChartKey()) {
          this.selectedChartKey.set(d.charts[0].key);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
