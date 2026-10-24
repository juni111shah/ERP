import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { I18nService } from '../core/i18n/i18n.service';
import { SessionStore } from '../core/session/session.store';

export type StatFormat = 'money' | 'number' | 'percent' | 'text';
export type StatTone = number | 'danger' | 'success' | 'warning' | 'neutral';

/** Palette of the Overview "Executive Highlights" cards: stripe colour + value colour, picked by index. */
export const STAT_PALETTES = [
  { stripe: '#10955d', text: '#0d7b4c' }, // Emerald brand green
  { stripe: '#2563eb', text: '#1d4ed8' }, // Indigo blue
  { stripe: '#0ea5e9', text: '#0284c7' }, // Cyan
  { stripe: '#f59e0b', text: '#d97706' }, // Amber
  { stripe: '#8b5cf6', text: '#7c3aed' }, // Purple
  { stripe: '#ec4899', text: '#e11d48' }, // Rose
];
const NAMED: Record<string, { stripe: string; text: string }> = {
  danger: { stripe: '#ef4444', text: '#cf1322' },
  success: { stripe: '#10955d', text: '#0d7b4c' },
  warning: { stripe: '#f59e0b', text: '#d97706' },
  neutral: { stripe: '#94a3b8', text: 'var(--erp-color-text-primary, #0f172a)' },
};

/**
 * Interpolates all numbers inside a formatted string from 0 to target based on progress.
 * E.g. "AED 1,234.50" with progress 0.5 -> "AED 617.25"
 * E.g. "AED 1,200.00 · USD 500.00" -> "AED 600.00 · USD 250.00"
 */
function formatAnimatedString(template: string, progress: number): string {
  if (progress >= 1) return template;
  return template.replace(/-?\d[\d,]*(\.\d+)?/g, (match) => {
    const cleanNumStr = match.replace(/,/g, '');
    const target = Number(cleanNumStr);
    if (isNaN(target) || target === 0) return match;

    const current = target * progress;
    const hasCommas = match.includes(',');
    const decIndex = match.indexOf('.');
    const decimals = decIndex !== -1 ? match.length - decIndex - 1 : 0;

    let formatted: string;
    if (decimals > 0) {
      formatted = current.toLocaleString('en-US', {
        minimumFractionDigits: decimals,
        maximumFractionDigits: decimals,
      });
    } else {
      formatted = Math.round(current).toLocaleString('en-US');
    }

    if (!hasCommas) {
      formatted = formatted.replace(/,/g, '');
    }

    return formatted;
  });
}

/**
 * The one stat / metric card of the app (look of the Overview "Executive Highlights").
 *   <erp-stat-card label="Total invoiced" [value]="stats().invoiced" [tone]="0" />
 * - Supports raw numbers or pre-formatted currency/number strings.
 * - Smooth count-up stopwatch animation applies on mount and updates across all app modules.
 * - Tabular numbers ensure zero horizontal jitter during count-up animation.
 * - Automatically respects `prefers-reduced-motion`.
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
export class StatCardComponent implements OnDestroy {
  private readonly i18n = inject(I18nService);
  private readonly session = inject(SessionStore, { optional: true });
  private readonly cdr = inject(ChangeDetectorRef);

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

  private animFrameId: number | null = null;
  readonly animProgress = signal<number>(1);
  private lastTargetNum = 0;
  private startNum = 0;

  readonly colors = computed(() => {
    const t = this.tone();
    return typeof t === 'number' ? STAT_PALETTES[((t % STAT_PALETTES.length) + STAT_PALETTES.length) % STAT_PALETTES.length] : NAMED[t];
  });

  constructor() {
    effect(() => {
      const v = this.value();
      const isLoading = this.loading();

      if (isLoading || v === null || v === undefined || v === '') {
        this.stopAnimation();
        this.animProgress.set(1);
        return;
      }

      // Check SSR or reduced motion preference
      if (typeof window === 'undefined' || window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches) {
        this.animProgress.set(1);
        return;
      }

      this.triggerAnimation(v);
    });
  }

  private triggerAnimation(v: number | string): void {
    this.stopAnimation();

    if (typeof v === 'number') {
      const prev = this.lastTargetNum;
      this.startNum = prev;
      this.lastTargetNum = v;
      if (prev === v) {
        this.animProgress.set(1);
        return;
      }
    } else {
      if (!/\d/.test(v)) {
        this.animProgress.set(1);
        return;
      }
      this.startNum = 0;
    }

    this.animProgress.set(0);

    const duration = 1000;
    const startTime = performance.now();
    const easeOutCubic = (t: number) => 1 - Math.pow(1 - t, 3);

    const step = (currentTime: number) => {
      const elapsed = currentTime - startTime;
      const rawProgress = Math.min(elapsed / duration, 1);
      const eased = easeOutCubic(rawProgress);

      this.animProgress.set(eased);
      this.cdr.markForCheck();

      if (rawProgress < 1) {
        this.animFrameId = requestAnimationFrame(step);
      } else {
        this.animProgress.set(1);
        this.animFrameId = null;
        this.cdr.markForCheck();
      }
    };

    this.animFrameId = requestAnimationFrame(step);
  }

  private stopAnimation(): void {
    if (this.animFrameId !== null && typeof cancelAnimationFrame !== 'undefined') {
      cancelAnimationFrame(this.animFrameId);
      this.animFrameId = null;
    }
  }

  ngOnDestroy(): void {
    this.stopAnimation();
  }

  readonly text = computed(() => {
    const v = this.value();
    if (v === null || v === undefined || v === '') return '—';

    const progress = this.animProgress();

    // 1. If v is a number
    if (typeof v === 'number') {
      const currentVal = progress >= 1 ? v : this.startNum + (v - this.startNum) * progress;

      switch (this.format()) {
        case 'percent':
          return `${this.i18n.number(currentVal, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
        case 'number':
          return this.i18n.number(Number.isInteger(v) ? Math.round(currentVal) : currentVal);
        default: {
          const currency = this.currency() ?? this.session?.me()?.workspace?.baseCurrency ?? '';
          const isMillions = this.shortenMillions() && Math.abs(v) >= 1_000_000;
          const body = isMillions
            ? `${this.i18n.number(currentVal / 1_000_000, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}M`
            : this.i18n.number(Math.round(currentVal), { maximumFractionDigits: 0 });
          return `${currency} ${body}`.trim();
        }
      }
    }

    // 2. If v is a string
    const str = String(v);
    if (progress >= 1 || !/\d/.test(str)) {
      return str;
    }

    return formatAnimatedString(str, progress);
  });
}
