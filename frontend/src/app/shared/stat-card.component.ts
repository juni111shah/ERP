import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { I18nService } from '../core/i18n/i18n.service';
import { SessionStore } from '../core/session/session.store';

export type StatFormat = 'money' | 'number' | 'percent' | 'text';
export type StatTone = number | 'danger' | 'success' | 'warning' | 'neutral';

/** Palette of the Overview "Executive Highlights" cards: stripe colour + value colour, picked by index. */
export const STAT_PALETTES = [
  { stripe: '#ec4899', text: '#e11d48' },
  { stripe: '#8b5cf6', text: '#7c3aed' },
  { stripe: '#0ea5e9', text: '#0284c7' },
  { stripe: '#f59e0b', text: '#d97706' },
  { stripe: '#10b981', text: '#059669' },
  { stripe: '#2563eb', text: '#1d4ed8' },
];
const NAMED: Record<string, { stripe: string; text: string }> = {
  danger: { stripe: '#ef4444', text: '#cf1322' },
  success: { stripe: '#10b981', text: '#059669' },
  warning: { stripe: '#f59e0b', text: '#d97706' },
  neutral: { stripe: '#94a3b8', text: 'var(--erp-color-text-primary, #0f172a)' },
};

/**
 * The one stat / metric card of the app (look of the Overview "Executive Highlights").
 *   <erp-stat-card label="Total invoiced" [value]="1234.5" format="money" currency="AED" caption="12 on this page" [tone]="2" link="/finance/invoices" />
 * - `format`: money | number | percent format `value` for the active language (Intl: grouping and digits follow EN/AR);
 *   `text` shows `value` as given (use it for pre-formatted strings).
 * - `loading` shows an ng-zorro skeleton instead of the numbers, so a page never flashes zeros before its data arrives.
 * - `compact` is the one-line variant for dense metric grids. Extra content (a progress bar) can be projected.
 * Numbers are wrapped in <bdi dir="ltr"> so they stay intact inside right-to-left text.
 */
@Component({
  selector: 'erp-stat-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, RouterLink, NzSkeletonModule],
  template: `
    @if (link(); as l) {
      <a class="erp-stat-card erp-stat-card--link" [class.erp-stat-card--compact]="compact()" [routerLink]="l" [attr.data-testid]="testId()"><ng-container *ngTemplateOutlet="body" /></a>
    } @else {
      <div class="erp-stat-card" [class.erp-stat-card--compact]="compact()" [attr.data-testid]="testId()"><ng-container *ngTemplateOutlet="body" /></div>
    }
    <ng-template #body>
      @if (!compact()) { <div class="erp-stat-stripe" [style.background]="colors().stripe"></div> }
      @if (loading()) {
        <nz-skeleton class="erp-stat-skeleton" [nzActive]="true" [nzTitle]="false" [nzParagraph]="{ rows: compact() ? 1 : 2, width: ['60%', '40%'] }" data-testid="stat-skeleton"></nz-skeleton>
      } @else {
        <div class="erp-stat-text">
          <div class="erp-stat-label">{{ label() }}</div>
          @if (compact() && caption()) { <div class="erp-stat-caption">{{ caption() }}</div> }
          @if (!compact()) {
            <div class="erp-stat-value" [style.color]="colors().text"><bdi dir="ltr">{{ text() }}</bdi>@if (suffix()) {<span class="erp-stat-suffix">{{ suffix() }}</span>}</div>
          }
        </div>
        @if (compact()) { <div class="erp-stat-value"><bdi dir="ltr">{{ text() }}</bdi>@if (suffix()) {<span class="erp-stat-suffix">{{ suffix() }}</span>}</div> }
        @if (!compact() && caption()) { <div class="erp-stat-caption">{{ caption() }}</div> }
        <div class="erp-stat-extra"><ng-content /></div>
      }
    </ng-template>`,
})
export class StatCardComponent {
  private readonly i18n = inject(I18nService);
  private readonly session = inject(SessionStore, { optional: true });

  readonly label = input.required<string>();
  readonly value = input<number | string | null | undefined>(null);
  readonly format = input<StatFormat>('text');
  /** Currency code shown before a `money` value; defaults to the workspace base currency. Pass '' for none. */
  readonly currency = input<string | null>(null);
  readonly suffix = input<string | null>(null);
  readonly caption = input<string | null>(null);
  readonly tone = input<StatTone>(0);
  readonly link = input<string | unknown[] | null>(null);
  readonly loading = input(false);
  readonly compact = input(false);
  readonly testId = input<string | null>(null);
  /** `money` values from 1 million up are shortened to 1.2M. */
  readonly shortenMillions = input(true);

  readonly colors = computed(() => {
    const t = this.tone();
    return typeof t === 'number' ? STAT_PALETTES[((t % STAT_PALETTES.length) + STAT_PALETTES.length) % STAT_PALETTES.length] : NAMED[t];
  });

  readonly text = computed(() => {
    const v = this.value();
    if (v === null || v === undefined || v === '') return '—';
    if (this.format() === 'text' || typeof v === 'string') return String(v);
    switch (this.format()) {
      case 'percent': return `${this.i18n.number(v, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
      case 'number': return this.i18n.number(v);
      default: {
        const currency = this.currency() ?? this.session?.me()?.workspace?.baseCurrency ?? '';
        const body = this.shortenMillions() && Math.abs(v) >= 1_000_000
          ? `${this.i18n.number(v / 1_000_000, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}M`
          : this.i18n.number(v, { maximumFractionDigits: 0 });
        return `${currency} ${body}`.trim();
      }
    }
  });
}
