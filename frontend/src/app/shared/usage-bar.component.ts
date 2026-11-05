import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { usagePercent } from '../core/format/subscription';

/** How much of a plan limit is used. A limit that is full turns amber; no limit shows only the count. */
@Component({
  selector: 'erp-usage-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    :host { display: block; }
    .row { display: flex; justify-content: space-between; gap: 12px; font-size: 13px; margin-bottom: 4px; }
    .label { color: var(--erp-color-text-secondary); }
    .value { font-weight: 600; color: var(--erp-color-text-primary); white-space: nowrap; font-variant-numeric: tabular-nums; }
    .track { height: 8px; border-radius: 9999px; background: var(--erp-color-bg-subtle); overflow: hidden; }
    .fill { height: 100%; border-radius: 9999px; background: var(--erp-color-primary); transition: width .3s ease; }
    .fill.full { background: var(--erp-color-warning-solid); }
  `],
  template: `
    <div class="row"><span class="label">{{ label() }}</span><span class="value">{{ used() }}{{ max() ? ' of ' + max() : '' }}@if (!max()) { <span class="label"> (no limit)</span> }</span></div>
    @if (max()) {
      <div class="track" role="progressbar" [attr.aria-label]="label()" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100">
        <div class="fill" [class.full]="percent() >= 100" [style.width.%]="percent()"></div>
      </div>
    }`,
})
export class UsageBarComponent {
  readonly label = input.required<string>();
  readonly used = input.required<number>();
  readonly max = input<number | null | undefined>(null);
  readonly percent = computed(() => usagePercent(this.used(), this.max()));
}
