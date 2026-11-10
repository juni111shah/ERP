import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { I18nService } from '@core/i18n/i18n.service';
import { SessionStore } from '@core/session/session.store';
import { StatFormat, StatTone, easeOutCubic, formatAnimatedString, statColors } from './stat-card-format';

export { STAT_PALETTES } from './stat-card-format';
export type { StatFormat, StatTone } from './stat-card-format';

/** How long the count-up takes, in milliseconds. */
const COUNT_UP_MS = 1000;

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
  templateUrl: './stat-card.component.html',
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

  readonly colors = computed(() => statColors(this.tone()));

  readonly text = computed(() => {
    const v = this.value();
    if (v === null || v === undefined || v === '') return '—';
    const progress = this.animProgress();
    if (typeof v === 'number') return this.numberText(v, progress >= 1 ? v : this.startNum + (v - this.startNum) * progress);
    const str = String(v);
    return progress >= 1 || !/\d/.test(str) ? str : formatAnimatedString(str, progress);
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

  ngOnDestroy(): void {
    this.stopAnimation();
  }

  /** A number in the card's format, showing `current` (the counted-up value) on the way to `v`. */
  private numberText(v: number, current: number): string {
    switch (this.format()) {
      case 'percent':
        return `${this.i18n.number(current, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
      case 'number':
        return this.i18n.number(Number.isInteger(v) ? Math.round(current) : current);
      default: {
        const currency = this.currency() ?? this.session?.me()?.workspace?.baseCurrency ?? '';
        const isMillions = this.shortenMillions() && Math.abs(v) >= 1_000_000;
        const body = isMillions
          ? `${this.i18n.number(current / 1_000_000, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}M`
          : this.i18n.number(Math.round(current), { maximumFractionDigits: 0 });
        return `${currency} ${body}`.trim();
      }
    }
  }

  /** Counts up from the previous number (or from zero for text) to the new value. */
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
    const startTime = performance.now();

    const step = (currentTime: number) => {
      const rawProgress = Math.min((currentTime - startTime) / COUNT_UP_MS, 1);
      this.animProgress.set(easeOutCubic(rawProgress));
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
}
