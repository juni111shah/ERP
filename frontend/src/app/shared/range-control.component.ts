import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { DashboardRange, RANGE_PRESETS, RangePreset, isoDate, parseIso, presetRange, validRange } from '../pages/dashboard.page';

/**
 * The period control of the overview, for any page that shows figures for a period: a dropdown of presets, a date-range picker for
 * "Custom" (up to 5 years, ending today or earlier) and a refresh button. It uses the overview's own period rules and wording.
 */
@Component({
  selector: 'erp-range-control',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, NzButtonModule, NzDatePickerModule, NzIconModule, NzSelectModule],
  styles: [`
    .range-control { display: inline-flex; align-items: center; flex-wrap: wrap; gap: var(--erp-space-2, 8px); }
    .range-select { min-width: 150px; } .range-picker { width: 260px; }
    .range-error { font-size: 12px; color: var(--erp-color-error-text); }
    @media (hover: none) { .range-control .range-btn-label { display: none; } }
    @media (max-width: 767px) { .range-control { width: 100%; } .range-select { flex: 1 1 140px; } .range-picker { flex: 1 1 100%; width: auto; } }
  `],
  template: `
    <div class="range-control" role="group" [attr.aria-label]="'ui.overview.range.group' | translate: 'Date range'" data-testid="dashboard-range">
      <nz-select class="range-select" [ngModel]="selectValue()" (ngModelChange)="setPreset($event)" [nzDropdownMatchSelectWidth]="false"
                 [attr.aria-label]="'ui.overview.range.group' | translate: 'Date range'" data-testid="dashboard-range-preset">
        @for (p of presets; track p.key) { <nz-option [nzValue]="p.key" [nzLabel]="p.labelKey | translate: p.label"></nz-option> }
        <nz-option nzValue="custom" [nzLabel]="'ui.overview.range.custom' | translate: 'Custom'"></nz-option>
      </nz-select>
      @if (showPicker()) {
        <nz-range-picker class="range-picker" nzFormat="dd MMM yyyy" [nzInputReadOnly]="true" [ngModel]="dates()" (ngModelChange)="setCustom($event)" [nzAllowClear]="false"
                         [nzDisabledDate]="disabledDate" [nzPlaceHolder]="placeholders()" data-testid="dashboard-range-picker"></nz-range-picker>
      }
      <button nz-button type="button" (click)="refresh.emit()" [attr.aria-label]="'ui.overview.range.refresh' | translate: 'Refresh'" data-testid="dashboard-range-refresh">
        <span nz-icon nzType="reload" [nzSpin]="loading()"></span><span class="range-btn-label">{{ 'ui.overview.range.refresh' | translate: 'Refresh' }}</span>
      </button>
      @if (error(); as e) { <span class="range-error" role="alert" data-testid="dashboard-range-error">{{ e }}</span> }
    </div>`,
})
export class RangeControlComponent {
  private readonly i18n = inject(I18nService);
  readonly range = input.required<DashboardRange>();
  readonly loading = input(false);
  readonly rangeChange = output<DashboardRange>();
  readonly refresh = output<void>();

  readonly presets = RANGE_PRESETS;
  readonly customPending = signal(false);
  readonly error = signal<string | null>(null);
  readonly showPicker = computed(() => this.range().preset === 'custom' || this.customPending());
  readonly selectValue = computed<RangePreset>(() => (this.showPicker() ? 'custom' : this.range().preset));
  readonly dates = computed(() => [parseIso(this.range().from), parseIso(this.range().to)]);
  readonly placeholders = computed(() => [this.i18n.t('ui.overview.range.from', 'Start date'), this.i18n.t('ui.overview.range.to', 'End date')]);
  /** No future dates. */
  readonly disabledDate = (d: Date): boolean => { const n = new Date(); return d > new Date(n.getFullYear(), n.getMonth(), n.getDate()); };

  setPreset(value: RangePreset): void {
    this.error.set(null);
    if (value === 'custom') { this.customPending.set(true); return; }
    this.customPending.set(false);
    this.rangeChange.emit(presetRange(value));
  }

  setCustom(dates: Date[] | null): void {
    if (!dates || dates.length < 2 || !dates[0] || !dates[1]) return;
    const from = isoDate(dates[0]), to = isoDate(dates[1]);
    if (!validRange(from, to)) {
      this.error.set(this.i18n.t('ui.overview.range.maxSpan', 'Pick a range of 5 years or less, ending today or earlier'));
      return;
    }
    this.error.set(null);
    this.customPending.set(false);
    this.rangeChange.emit({ preset: 'custom', from, to });
  }
}
