import { TranslatePipe } from '../core/i18n/translate.pipe';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { DashboardApi, DashboardDto, KpiCard } from '../core/api/admin.api';
import { SessionStore } from '../core/session/session.store';
import { SimpleChartComponent } from '../shared/simple-chart.component';

const URGENCY: Record<string, { label: string; color: string }> = {
  action: { label: 'Action needed', color: 'red' }, upcoming: { label: 'Upcoming', color: 'gold' }, info: { label: 'For information', color: 'blue' },
};

/** Overview: a hero banner, KPI cards, charts and an alerts panel, all built by the server from core and the enabled modules. */
@Component({
  selector: 'erp-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DecimalPipe, RouterLink, SimpleChartComponent, NzIconModule, NzSpinModule, NzTagModule],
  styles: [`
    .hero { background: linear-gradient(90deg, #142a41, #575345); color:#fff; border-radius:12px; padding:24px 28px; margin-bottom:20px }
    .hero .eyebrow { letter-spacing:.12em; font-size:11px; color:#bfa66a; text-transform:uppercase }
    .hero h1 { color:#fff; margin:6px 0 4px; font-size:26px } .hero p { margin:0; opacity:.85 }
    .grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap:16px; margin-bottom:20px }
    .kpi { background:var(--erp-color-surface, #fff); border:1px solid var(--erp-color-border, #e5e8ec); border-top:3px solid #bfa66a; border-radius:8px; padding:14px 16px; display:block; color:inherit }
    .kpi .label { font-size:12px; color: var(--erp-color-text-secondary, #667); text-transform:uppercase; letter-spacing:.04em } .kpi .value { font-size:24px; font-weight:600; margin:4px 0 } .kpi .caption { font-size:12px; color: var(--erp-color-text-secondary, #667) }
    .layout { display:grid; grid-template-columns: 1fr 340px; gap:20px } @media (max-width: 1100px) { .layout { grid-template-columns: 1fr } }
    .charts { display:grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap:16px; align-content:start }
    .panel { background:var(--erp-color-surface, #fff); border:1px solid var(--erp-color-border, #e5e8ec); border-radius:8px; padding:14px 16px } .panel h3 { margin:0 0 2px; font-size:15px } .panel .sub { font-size:12px; color: var(--erp-color-text-secondary, #667); margin-bottom:8px }
    .alerts { max-height: 640px; overflow:auto } .alert { display:flex; gap:10px; padding:10px 0; border-bottom:1px solid var(--erp-color-border, #eee); color:inherit; align-items:flex-start } .alert:last-child { border:0 } .alert .msg { flex:1; font-size:13px }
    .critical { color:#cf1322 } .warning { color:#d48806 } .info { color:#1677ff }
  `],
  template: `
    <div class="hero">
      <div class="eyebrow">{{ session.me()?.workspace?.name }} · overview</div>
      <h1 data-testid="dashboard-title">{{ 'dashboard.welcome' | translate: 'Welcome' }}, {{ session.me()?.user?.fullName }}</h1>
      <p>Every figure is built from posted transactions · as of {{ data()?.asOf }}.</p>
    </div>
    <nz-spin [nzSpinning]="loading()">
      @if (data(); as d) {
        <div class="grid" data-testid="kpi-grid">
          @for (k of d.kpis; track k.key) {
            <a class="kpi" [routerLink]="k.link ?? null" [attr.data-testid]="'kpi-' + k.key">
              <div class="label">{{ k.label }}</div>
              <div class="value">{{ show(k) }}</div>
              @if (k.caption) { <div class="caption">{{ k.caption }}</div> }
            </a>
          }
        </div>
        <div class="layout">
          <div class="charts">
            @for (c of d.charts; track c.key) {
              <div class="panel" [attr.data-testid]="'chart-' + c.key"><h3>{{ c.title }}</h3>@if (c.subtitle) { <div class="sub">{{ c.subtitle }}</div> }<erp-simple-chart [chart]="c" /></div>
            }
          </div>
          <div class="panel alerts" data-testid="alerts">
            <h3>{{ 'dashboard.alerts' | translate: 'Alerts and to-dos' }}</h3>
            @for (a of d.alerts; track $index) {
              <a class="alert" [routerLink]="a.link ?? null">
                <span nz-icon [nzType]="a.severity === 'info' ? 'bell' : 'bulb'" [class]="a.severity"></span>
                <span class="msg"><nz-tag>{{ a.category }}</nz-tag><nz-tag [nzColor]="urgency(a.urgency).color">{{ urgency(a.urgency).label }}</nz-tag><br />{{ a.message }}</span>
              </a>
            } @empty { <div class="sub">Nothing needs your attention.</div> }
          </div>
        </div>
      }
    </nz-spin>`,
})
export class DashboardPage {
  readonly session = inject(SessionStore);
  private readonly api = inject(DashboardApi);
  readonly data = signal<DashboardDto | null>(null);
  readonly loading = signal(false);
  private readonly money = computed(() => new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }));

  constructor() {
    // reload whenever the legal entity switcher changes
    effect(() => { this.session.currentEntityId(); untracked(() => this.load()); });
  }

  urgency(u: string): { label: string; color: string } { return URGENCY[u] ?? URGENCY['info']; }

  show(k: KpiCard): string {
    if (k.format === 'percent') return `${k.value.toFixed(1)}%`;
    if (k.format === 'number') return k.value.toLocaleString();
    const cur = this.session.me()?.workspace?.baseCurrency ?? '';
    const abs = Math.abs(k.value);
    const text = abs >= 1_000_000 ? `${(k.value / 1_000_000).toFixed(1)}M` : this.money().format(k.value);
    return `${cur} ${text}`.trim();
  }

  private load(): void {
    this.loading.set(true);
    this.api.get().subscribe({ next: (d) => { this.data.set(d); this.loading.set(false); }, error: () => this.loading.set(false) });
  }
}
