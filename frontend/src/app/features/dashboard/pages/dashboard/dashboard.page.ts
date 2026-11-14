import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerComponent, NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { DashboardAlert, DashboardApi, DashboardChart, DashboardDto, KpiCard } from '@core/api/admin.api';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { StatCardComponent, StatFormat } from '@shared/components/stat-card/stat-card.component';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { UrgencyTag, alertsFor, readableAlert, urgencyTag } from '../../dashboard-alerts';
import { allowedLink } from '../../dashboard-links';
import {
  DomainCategory, FINANCE_CHARTS, KpiCluster, MATRIX_CLUSTER_TITLES, OPS_CHARTS, OVERVIEW_CLUSTER_TITLES, SALES_CHARTS,
  categoryCharts, categoryKpis, categoryTitle, chartsIn, classifyKpi, heroKpis, kpiClusters, pickChart, searchKpis, withoutHeroes,
} from '../../dashboard-categories';
import { CaptionPart, browserTimezone, captionParts, formatIsoDate, isolate, latn, localizeChart, localizeKpi } from '../../dashboard-locale';
import {
  DEFAULT_PRESET, DashboardRange, RANGE_PRESETS, RangePreset, blockedDay, initialRange, isoDate, parseIso, presetRange, rangeFromParams,
  rangeParams, rememberRange, sameRange, storedRange, validRange,
} from '@shared/utils/date-range';

/** The Overview: one period for every card and chart, the hero KPIs, and domain tabs with their KPIs, charts and alerts. */
@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe,
    TranslatePipe,
    RouterLink,
    FormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzSelectModule,
    NzToolTipModule,
    RevealDirective,
    ApexChartComponent,
    StatCardComponent,
    NzIconModule,
    NzSkeletonModule,
    NzSpinModule,
    NzTagModule,
  ],
  styleUrl: './dashboard.page.scss',
  templateUrl: './dashboard.page.html',
})
export class DashboardPage {
  readonly session = inject(SessionStore);
  private readonly dashboardApi = inject(DashboardApi);
  private readonly i18n = inject(I18nService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /* ---- Period ---- */

  /**
   * One shared period for every card and chart: URL (?range= or ?from=&to=) wins over the browser's last choice, then the "Last year"
   * default (the trailing 12 months). Picked from one dropdown; "Custom" shows a date-range picker, a preset hides it again.
   */
  readonly presets = RANGE_PRESETS;
  readonly range = signal<DashboardRange>(initialRange(this.route.snapshot.queryParamMap));
  /** "Custom" chosen in the dropdown but no dates picked yet: the picker shows (and opens) while the figures keep the current period. */
  readonly customPending = signal(false);
  readonly showPicker = computed(() => this.range().preset === 'custom' || this.customPending());
  readonly selectValue = computed<RangePreset>(() => (this.showPicker() ? 'custom' : this.range().preset));
  private readonly picker = viewChild(NzDatePickerComponent);
  /** Why the last picked range was rejected (shown inline under the picker), or null. */
  readonly rangeError = signal<string | null>(null);
  /** The first date clicked in the open picker, so dates more than 5 years away from it are disabled. */
  private readonly calendarAnchor = signal<Date | null>(null);
  /** Bumped to hand the picker the current period again after a rejected pick (without reloading the figures). */
  private readonly pickerReset = signal(0);
  readonly rangeDates = computed<Date[]>(() => { this.pickerReset(); return [parseIso(this.range().from), parseIso(this.range().to)]; });
  readonly rangePlaceholders = computed(() => [this.i18n.t('ui.overview.range.from', 'Start date'), this.i18n.t('ui.overview.range.to', 'End date')]);
  readonly maxSpanHint = computed(() => this.i18n.t('ui.overview.range.maxSpan', 'Pick a range of 5 years or less, ending today or earlier'));
  /** No future dates, and once a first date is picked nothing that would make the period longer than 5 years. */
  readonly disabledDate = (d: Date): boolean => blockedDay(d, this.calendarAnchor());
  /** "30 Oct 2025 – 30 Oct 2026" / "من 31 أكتوبر 2025 إلى 30 أكتوبر 2026", with each date bidi-isolated. */
  readonly captionParts = computed<CaptionPart[]>(() => {
    const r = this.range();
    return captionParts(this.i18n.t('ui.overview.range.caption', '{from} – {to}'), r.from, r.to, this.locale());
  });
  readonly canShowLonger = computed(() => this.range().from > presetRange('3y').from);
  readonly timezoneLabel = computed(() => browserTimezone());

  /* ---- Figures ---- */

  /** The API response as received; `data` is the same with captions and chart axis labels in the user's language. */
  private readonly raw = signal<DashboardDto | null>(null);
  readonly loadError = signal(false);
  readonly loading = signal(false);
  readonly data = computed<DashboardDto | null>(() => {
    const d = this.raw();
    if (!d) return null;
    const locale = this.locale();
    const translate = (key: string) => this.i18n.t(key, '');
    // A card or alert links only to a page this person may open; it follows the session, so changed access re-checks the links.
    const can = (p: string) => this.session.hasPermission(p);
    return {
      ...d,
      kpis: d.kpis.map((k) => localizeKpi({ ...k, link: allowedLink(k.link, can) ?? undefined }, locale, translate)),
      charts: d.charts.map((c) => localizeChart(c, locale)),
      alerts: d.alerts.map((a) => ({ ...a, link: allowedLink(a.link, can) ?? undefined })),
    };
  });
  /** Screen-reader announcement once the figures for a period have loaded. */
  readonly announcement = computed(() => {
    const d = this.raw();
    if (!d?.from || !d.to) return '';
    const locale = this.locale();
    return this.i18n.t('ui.overview.range.showing', 'Showing {from} – {to}')
      .replace('{from}', isolate(formatIsoDate(d.from, locale))).replace('{to}', isolate(formatIsoDate(d.to, locale)));
  });
  /** Nothing billed or collected in the period: say so instead of showing an empty chart without explanation. */
  readonly periodEmpty = computed(() => {
    const c = this.raw()?.charts.find((x) => x.key === 'billed-collected');
    return !!c && c.series.every((s) => s.values.every((v) => v === 0));
  });
  readonly alertText = readableAlert;

  /* ---- Domain tabs, KPIs and charts ---- */

  readonly activeCategory = signal<DomainCategory>('all');
  readonly viewMode = signal<'curated' | 'all'>('curated');
  readonly selectedChartKey = signal<string | null>(null);
  readonly selectedFinanceChartKey = signal<string>('billed-collected');
  readonly selectedSalesChartKey = signal<string>('sales-pipeline');
  readonly selectedOpsChartKey = signal<string>('purchasing-orders-status');
  readonly selectedDomainChartKey = signal<string | null>(null);
  readonly metricSearchQuery = signal<string>('');

  private readonly kpis = computed<KpiCard[]>(() => this.data()?.kpis ?? []);
  private readonly charts = computed<DashboardChart[]>(() => this.data()?.charts ?? []);

  readonly activeCategoryKpis = computed<KpiCard[]>(() => this.getCategoryKpis(this.activeCategory()));
  readonly filteredMatrixKpis = computed<KpiCard[]>(() => searchKpis(this.activeCategoryKpis(), this.metricSearchQuery()));
  readonly allMatrixClusters = computed<KpiCluster[]>(() => kpiClusters(this.kpis(), MATRIX_CLUSTER_TITLES));
  readonly activeCategoryTitle = computed(() => categoryTitle(this.activeCategory()));
  /** 4 Hero Highlights for All Overview */
  readonly heroKpis = computed<KpiCard[]>(() => heroKpis(this.kpis()));
  /** Hero KPIs depending on selected category tab */
  readonly activeHeroKpis = computed<KpiCard[]>(() =>
    this.activeCategory() === 'all' ? this.heroKpis() : this.getCategoryKpis(this.activeCategory()).slice(0, 4));
  /** Remaining KPIs for single domain category */
  readonly activeRemainingKpis = computed<KpiCard[]>(() => withoutHeroes(this.getCategoryKpis(this.activeCategory()), this.activeHeroKpis()));
  /** Clean Departmental Clusters for All Overview mode; the heroes are left out so they are not shown twice. */
  readonly clusters = computed<KpiCluster[]>(() => kpiClusters(withoutHeroes(this.kpis(), this.heroKpis()), OVERVIEW_CLUSTER_TITLES));

  readonly visibleCharts = computed<DashboardChart[]>(() => this.getCategoryCharts(this.activeCategory()));
  readonly financeCharts = computed<DashboardChart[]>(() => chartsIn(this.charts(), FINANCE_CHARTS));
  readonly currentFinanceChart = computed<DashboardChart | null>(() => pickChart(this.financeCharts(), this.selectedFinanceChartKey()));
  readonly salesCharts = computed<DashboardChart[]>(() => chartsIn(this.charts(), SALES_CHARTS));
  readonly currentSalesChart = computed<DashboardChart | null>(() => pickChart(this.salesCharts(), this.selectedSalesChartKey()));
  readonly opsCharts = computed<DashboardChart[]>(() => chartsIn(this.charts(), OPS_CHARTS));
  readonly currentOpsChart = computed<DashboardChart | null>(() => pickChart(this.opsCharts(), this.selectedOpsChartKey()));
  readonly currentDomainChart = computed<DashboardChart | null>(() => pickChart(this.visibleCharts(), this.selectedDomainChartKey()));
  readonly currentChart = computed<DashboardChart | null>(() => pickChart(this.visibleCharts(), this.selectedChartKey()));
  readonly activeDomainAlerts = computed<DashboardAlert[]>(() => alertsFor(this.data()?.alerts ?? [], this.activeCategory()));

  private pending?: Subscription;

  constructor() {
    effect(() => {
      this.session.currentEntityId();
      const range = this.range();
      untracked(() => this.load(range));
    });
    // Back/forward and shared links follow the URL. With no (or invalid) range params the period comes from this browser's last choice,
    // then the default, and is written back to the URL without a new history entry so the address always names the period shown.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((p) => {
      const fromUrl = rangeFromParams(p);
      const r = fromUrl ?? storedRange() ?? presetRange(DEFAULT_PRESET);
      if (!sameRange(r, this.range())) { this.customPending.set(false); this.rangeError.set(null); this.range.set(r); }
      if (!fromUrl) void this.router.navigate([], { relativeTo: this.route, queryParams: rangeParams(r), queryParamsHandling: 'merge', replaceUrl: true });
    });
  }

  /** Reloads the figures. A preset is re-anchored on today first, so a page left open past midnight moves on to the new day. */
  refresh(): void {
    const r = this.range();
    if (r.preset !== 'custom') {
      const fresh = presetRange(r.preset);
      if (!sameRange(fresh, r)) { this.saveRange(fresh); return; }
    }
    this.load(r);
  }

  /** A preset applies at once and hides the picker; "Custom" only shows the picker and opens it (no request until dates are picked). */
  setPreset(preset: RangePreset): void {
    this.rangeError.set(null);
    if (preset === 'custom') {
      this.customPending.set(true);
      setTimeout(() => this.picker()?.open());
      return;
    }
    this.customPending.set(false);
    this.saveRange(presetRange(preset));
  }

  onPickerOpenChange(_open: boolean): void { this.calendarAnchor.set(null); }

  onCalendarChange(dates: (Date | null)[] | null): void {
    const picked = (dates ?? []).filter((d): d is Date => !!d);
    this.calendarAnchor.set(picked.length === 1 ? picked[0] : null);
  }

  setCustomRange(dates: (Date | null)[] | null): void {
    const [a, b] = dates ?? [];
    if (!a || !b) return;
    const [from, to] = [isoDate(a), isoDate(b)].sort();
    if (!validRange(from, to)) {
      this.rangeError.set(this.maxSpanHint());
      this.pickerReset.update((n) => n + 1); // hands the picker the current period back, clearing the rejected dates
      return;
    }
    this.rangeError.set(null);
    this.customPending.set(false);
    this.saveRange({ preset: 'custom', from, to });
  }

  /** "by week" / "by month" / "by quarter" under a time-series chart. */
  bucketLabel(c: DashboardChart | null | undefined): string {
    const g = c?.granularity;
    return g ? this.i18n.t('ui.overview.range.bucket.' + g, 'by ' + g) : '';
  }

  setCategory(cat: DomainCategory): void {
    this.activeCategory.set(cat);
    const charts = this.getCategoryCharts(cat);
    if (charts.length > 0) {
      this.selectedDomainChartKey.set(charts[0].key);
      this.selectedChartKey.set(charts[0].key);
    }
  }

  selectFinanceChart(key: string): void {
    this.selectedFinanceChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectSalesChart(key: string): void {
    this.selectedSalesChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectOpsChart(key: string): void {
    this.selectedOpsChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  selectDomainChart(key: string): void {
    this.selectedDomainChartKey.set(key);
    this.selectedChartKey.set(key);
  }

  urgency(u: string): UrgencyTag {
    return urgencyTag(u);
  }

  statFormat(k: KpiCard): StatFormat {
    return k.format === 'percent' ? 'percent' : k.format === 'number' ? 'number' : 'money';
  }

  hasCategory(cat: DomainCategory): boolean {
    return this.kpis().some((k) => classifyKpi(k.key) === cat);
  }

  getCategoryKpis(cat: DomainCategory): KpiCard[] {
    return categoryKpis(this.kpis(), cat);
  }

  getCategoryCharts(cat: DomainCategory): DashboardChart[] {
    return categoryCharts(this.charts(), cat);
  }

  private locale(): string {
    return latn(this.i18n.locale());
  }

  private saveRange(r: DashboardRange): void {
    rememberRange(r);
    if (sameRange(r, this.range())) return;
    this.range.set(r);
    void this.router.navigate([], { relativeTo: this.route, queryParams: rangeParams(r), queryParamsHandling: 'merge' });
  }

  private load(range: DashboardRange = this.range()): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.pending?.unsubscribe(); // cancels the stale request when the period or entity changes again
    this.pending = this.dashboardApi.get({ from: range.from, to: range.to }).subscribe({
      next: (d) => {
        this.raw.set(d);
        this.selectFirstCharts(d.charts);
        this.loading.set(false);
      },
      // no endless skeleton and no figures from the previous period: show the error with a Retry instead
      error: () => { this.raw.set(null); this.loadError.set(true); this.loading.set(false); },
    });
  }

  /** After a load: the first chart overall (unless one is chosen already) and the first chart of each group. */
  private selectFirstCharts(charts: DashboardChart[]): void {
    if (charts.length === 0) return;
    if (!this.selectedChartKey()) this.selectedChartKey.set(charts[0].key);
    const fin = chartsIn(charts, FINANCE_CHARTS)[0];
    if (fin) this.selectedFinanceChartKey.set(fin.key);
    const sales = chartsIn(charts, SALES_CHARTS)[0];
    if (sales) this.selectedSalesChartKey.set(sales.key);
    const ops = chartsIn(charts, OPS_CHARTS)[0];
    if (ops) this.selectedOpsChartKey.set(ops.key);
  }
}
