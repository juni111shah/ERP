import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DashboardChart } from '../core/api/admin.api';

const COLORS = ['#2a78d6', '#eb6834', '#3aa76d', '#bfa66a', '#8e6bd6', '#6b7a90'];

/**
 * Small dependency-free SVG chart for the overview: grouped or stacked vertical bars, or horizontal bars. Values come from the
 * server (DashboardChart), so modules can add charts without any frontend code.
 */
@Component({
  selector: 'erp-simple-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  styles: [`:host { display:block } .legend { display:flex; gap:12px; flex-wrap:wrap; font-size:12px; margin-top:6px } .legend i { display:inline-block; width:10px; height:10px; border-radius:2px; margin-inline-end:4px } svg text { font-size:11px; fill: var(--erp-color-text-secondary, #666) } .empty { color: var(--erp-color-text-secondary, #888); padding: 24px 0 }`],
  template: `
    @if (!hasData()) { <div class="empty">Nothing to show yet.</div> } @else {
      @if (chart().kind === 'hbar') {
        <svg [attr.viewBox]="'0 0 520 ' + (rows().length * 26 + 8)" width="100%" role="img" [attr.aria-label]="chart().title">
          @for (r of rows(); track r.label; let i = $index) {
            <text x="0" [attr.y]="i * 26 + 16">{{ r.label }}</text>
            <rect x="130" [attr.y]="i * 26 + 4" [attr.width]="r.width" height="16" [attr.fill]="color(0)" rx="2"><title>{{ r.label }}: {{ r.value | number: '1.2-2' }}</title></rect>
            <text [attr.x]="134 + r.width" [attr.y]="i * 26 + 16">{{ r.value | number: '1.0-0' }}</text>
          }
        </svg>
      } @else {
        <svg viewBox="0 0 520 190" width="100%" role="img" [attr.aria-label]="chart().title">
          <line x1="0" y1="160" x2="520" y2="160" stroke="#ccc" />
          @for (b of bars(); track $index) {
            <rect [attr.x]="b.x" [attr.y]="b.y" [attr.width]="b.w" [attr.height]="b.h" [attr.fill]="color(b.series)" rx="1"><title>{{ b.title }}</title></rect>
          }
          @for (c of labels(); track $index) { <text [attr.x]="c.x" y="178" text-anchor="middle">{{ c.label }}</text> }
        </svg>
      }
      @if (chart().series.length > 1) {
        <div class="legend">@for (s of chart().series; track s.name; let i = $index) { <span><i [style.background]="color(i)"></i>{{ s.name }}</span> }</div>
      }
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
    return c.categories.map((label, i) => ({ label, value: values[i] ?? 0, width: Math.max(2, (Math.abs(values[i] ?? 0) / max) * 300) }));
  });

  private readonly slot = computed(() => 500 / Math.max(this.chart().categories.length, 1));

  readonly labels = computed(() => this.chart().categories.map((label, i) => ({ label, x: 10 + this.slot() * i + this.slot() / 2 })));

  readonly bars = computed(() => {
    const c = this.chart();
    const n = c.categories.length; const m = c.series.length; const slot = this.slot();
    const stacked = c.kind === 'stacked';
    const totals = c.categories.map((_, i) => c.series.reduce((s, x) => s + Math.max(x.values[i] ?? 0, 0), 0));
    const max = Math.max(stacked ? Math.max(...totals, 1) : Math.max(...c.series.flatMap((s) => s.values), 1), 1);
    const scale = 150 / max;
    const out: { x: number; y: number; w: number; h: number; series: number; title: string }[] = [];
    for (let i = 0; i < n; i++) {
      const pad = slot * 0.15; const inner = slot - pad * 2;
      let offset = 0;
      for (let s = 0; s < m; s++) {
        const v = Math.max(c.series[s].values[i] ?? 0, 0); const h = v * scale;
        const w = stacked ? inner : inner / m;
        out.push({ x: 10 + slot * i + pad + (stacked ? 0 : w * s), y: 160 - h - (stacked ? offset : 0), w, h, series: s, title: `${c.categories[i]} · ${c.series[s].name}: ${v.toLocaleString()}` });
        if (stacked) offset += h;
      }
    }
    return out;
  });
}
