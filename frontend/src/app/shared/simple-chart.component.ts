import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DashboardChart } from '../core/api/admin.api';

const COLORS = ['#2563eb', '#8b5cf6', '#0ea5e9', '#10b981', '#f59e0b', '#ec4899'];

/**
 * Clean, modern dependency-free SVG chart: rounded vertical bars with gridlines, or horizontal progress bars.
 * Values come from the server (DashboardChart), so modules can add charts without any frontend code.
 */
@Component({
  selector: 'erp-simple-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  styles: [`
    :host { display: block; width: 100%; }
    .chart-container { position: relative; width: 100%; }
    .legend { display: flex; gap: 16px; flex-wrap: wrap; font-size: 12px; margin-top: 10px; padding-left: 36px; }
    .legend-item { display: inline-flex; align-items: center; gap: 6px; color: var(--erp-color-text-secondary, #64748b); font-weight: 500; }
    .legend-dot { width: 8px; height: 8px; border-radius: 50%; }
    svg { overflow: visible; font-family: inherit; }
    svg text { font-size: 11px; fill: var(--erp-color-text-tertiary, #94a3b8); font-weight: 500; }
    .grid-line { stroke: var(--erp-color-border-subtle, #f1f5f9); stroke-width: 1; }
    .chart-bar { transition: transform 0.2s ease, opacity 0.2s ease; cursor: pointer; }
    .chart-bar:hover { opacity: 0.85; filter: drop-shadow(0 4px 6px rgba(37, 99, 235, 0.25)); }
    .empty { color: var(--erp-color-text-tertiary, #94a3b8); padding: 36px 0; text-align: center; font-size: 13px; }
  `],
  template: `
    @if (!hasData()) {
      <div class="empty">Nothing to show yet.</div>
    } @else {
      <div class="chart-container">
        @if (chart().kind === 'hbar') {
          <svg [attr.viewBox]="'0 0 540 ' + (rows().length * 32 + 16)" width="100%" role="img" [attr.aria-label]="chart().title">
            @for (r of rows(); track r.label; let i = $index) {
              <!-- Background track -->
              <rect x="140" [attr.y]="i * 32 + 8" width="300" height="14" fill="var(--erp-color-border-subtle, #f1f5f9)" rx="7" />
              <!-- Bar fill -->
              <rect class="chart-bar" x="140" [attr.y]="i * 32 + 8" [attr.width]="r.width" height="14" [attr.fill]="color(0)" rx="7">
                <title>{{ r.label }}: {{ r.value | number: '1.2-2' }}</title>
              </rect>
              <text x="0" [attr.y]="i * 32 + 19" text-anchor="start" font-weight="500">{{ r.label }}</text>
              <text [attr.x]="450" [attr.y]="i * 32 + 19" text-anchor="start" font-weight="600" fill="var(--erp-color-text-primary, #1e293b)">
                {{ r.value | number: '1.0-0' }}
              </text>
            }
          </svg>
        } @else {
          <svg viewBox="0 0 540 210" width="100%" role="img" [attr.aria-label]="chart().title">
            <!-- Gridlines & Y-axis labels -->
            @for (g of gridLines(); track $index) {
              <line x1="40" [attr.y1]="g.y" x2="530" [attr.y2]="g.y" class="grid-line" />
              <text x="32" [attr.y]="g.y + 4" text-anchor="end">{{ g.label }}</text>
            }
            <line x1="40" y1="175" x2="530" y2="175" stroke="var(--erp-color-border-subtle, #e2e8f0)" stroke-width="1.5" />

            <!-- Vertical Bars -->
            @for (b of bars(); track $index) {
              <rect class="chart-bar" [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.w" [attr.height]="b.h" [attr.fill]="color(b.series)" rx="4">
                <title>{{ b.title }}</title>
              </rect>
            }

            <!-- X-axis labels -->
            @for (c of labels(); track $index) {
              <text [attr.x]="c.x" y="196" text-anchor="middle">{{ c.label }}</text>
            }
          </svg>
        }

        @if (chart().series.length > 1) {
          <div class="legend">
            @for (s of chart().series; track s.name; let i = $index) {
              <span class="legend-item">
                <span class="legend-dot" [style.background]="color(i)"></span>
                <span>{{ s.name }}</span>
              </span>
            }
          </div>
        }
      </div>
    }`,
})
export class SimpleChartComponent {
  readonly chart = input.required<DashboardChart>();
  color(i: number): string { return COLORS[i % COLORS.length]; }

  readonly hasData = computed(() => this.chart().series.some((s) => s.values.some((v) => v !== 0)));

  readonly rows = computed(() => {
    const c = this.chart();
    const values = c.series[0]?.values ?? [];
    const max = Math.max(...values.map(Math.abs), 1);
    return c.categories.map((label, i) => ({
      label,
      value: values[i] ?? 0,
      width: Math.max(4, (Math.abs(values[i] ?? 0) / max) * 300),
    }));
  });

  private readonly maxValue = computed(() => {
    const c = this.chart();
    const stacked = c.kind === 'stacked';
    if (stacked) {
      const totals = c.categories.map((_, i) => c.series.reduce((s, x) => s + Math.max(x.values[i] ?? 0, 0), 0));
      return Math.max(...totals, 1);
    }
    return Math.max(...c.series.flatMap((s) => s.values.map((v) => Math.max(v, 0))), 1);
  });

  readonly gridLines = computed(() => {
    const max = this.maxValue();
    const steps = 4;
    const lines = [];
    for (let i = 0; i <= steps; i++) {
      const val = (max / steps) * (steps - i);
      const y = 35 + (140 / steps) * i;
      let label = val >= 1_000_000 ? `${(val / 1_000_000).toFixed(1)}M` : val >= 1_000 ? `${Math.round(val / 1_000)}K` : Math.round(val).toString();
      if (val === 0) label = '0';
      lines.push({ y, label });
    }
    return lines;
  });

  private readonly slot = computed(() => 470 / Math.max(this.chart().categories.length, 1));

  readonly labels = computed(() => this.chart().categories.map((label, i) => ({
    label,
    x: 45 + this.slot() * i + this.slot() / 2,
  })));

  readonly bars = computed(() => {
    const c = this.chart();
    const n = c.categories.length;
    const m = c.series.length;
    const slot = this.slot();
    const stacked = c.kind === 'stacked';
    const max = this.maxValue();
    const scale = 140 / max;
    const out: { x: number; y: number; w: number; h: number; series: number; title: string }[] = [];

    for (let i = 0; i < n; i++) {
      const pad = Math.max(slot * 0.18, 4);
      const inner = Math.min(slot - pad * 2, 36);
      const startX = 45 + slot * i + (slot - inner) / 2;
      let offset = 0;

      for (let s = 0; s < m; s++) {
        const v = Math.max(c.series[s].values[i] ?? 0, 0);
        const h = Math.max(v * scale, v > 0 ? 3 : 0);
        const w = stacked ? inner : inner / m;
        const xPos = stacked ? startX : startX + w * s;
        out.push({
          x: xPos,
          y: 175 - h - (stacked ? offset : 0),
          w: Math.max(w - 2, 4),
          h,
          series: s,
          title: `${c.categories[i]} · ${c.series[s].name}: ${v.toLocaleString()}`,
        });
        if (stacked) offset += h;
      }
    }
    return out;
  });
}

