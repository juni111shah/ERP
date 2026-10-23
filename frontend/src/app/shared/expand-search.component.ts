import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, model, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { I18nService } from '../core/i18n/i18n.service';

let nextId = 0;

/**
 * Expanding search (app-wide standard, styles in src/theme/_expand-search.scss). Shows only a search icon button; hovering hints with a
 * tooltip, clicking it (or Enter / Space when focused) expands the input with an animated width and focuses it. It collapses again when it
 * is empty and loses focus. Esc leaves the field: an empty field collapses, a field with text keeps its text and stays expanded so an
 * active filter is always visible (an active query never collapses). Expands toward the inline end (logical properties, so RTL mirrors).
 * The value is two-way (`[(value)]`, or `[value]` + `(valueChange)`), so debouncing / query params stay with the caller.
 */
@Component({
  selector: 'erp-expand-search',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzIconModule, NzInputModule, NzToolTipModule],
  host: { class: 'erp-xsearch', '[class.is-open]': 'open()', '[class.is-small]': "size() === 'small'", '[style.--erp-xsearch-width]': 'width()' },
  template: `
    <button type="button" class="erp-xsearch-btn" nz-tooltip [nzTooltipTitle]="open() ? null : name()"
            [attr.aria-label]="open() ? t('ui.common.closeSearch', 'Close search') : t('ui.common.openSearch', 'Open search')" [attr.aria-expanded]="open()" [attr.aria-controls]="inputId"
            (mousedown)="$event.preventDefault()" (click)="toggle()" [attr.data-testid]="testId() ? testId() + '-toggle' : null">
      <span nz-icon nzType="search" aria-hidden="true"></span>
    </button>
    <input #field nz-input type="search" class="erp-xsearch-input" [nzSize]="size()" [id]="inputId" [attr.inert]="open() ? null : ''" [attr.tabindex]="open() ? null : -1"
           [placeholder]="placeholder() || name()" [attr.aria-label]="name()" [ngModel]="value()" (ngModelChange)="value.set($event ?? '')"
           (focus)="expanded.set(true)" (blur)="onBlur()" (keydown.escape)="onEscape($event)" [attr.data-testid]="testId() || null" />`,
})
export class ExpandSearchComponent {
  private readonly i18n = inject(I18nService);
  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');
  /** Search text (two-way). */
  readonly value = model('');
  /** Visible placeholder; defaults to the accessible label. */
  readonly placeholder = input('');
  /** Accessible name and tooltip; defaults to "Search". */
  readonly label = input('');
  readonly testId = input('');
  readonly size = input<'default' | 'small'>('default');
  /** Expanded width (any CSS length). */
  readonly width = input('260px');
  readonly inputId = `erp-xsearch-${++nextId}`;
  /** opened by the user (click / focus); an active query keeps it open regardless */
  readonly expanded = signal(false);
  readonly open = computed(() => this.expanded() || !!this.value());
  readonly name = computed(() => this.label() || this.t('ui.common.search', 'Search'));

  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }

  toggle(): void {
    if (this.open() && !this.value() && document.activeElement === this.field().nativeElement) { this.expanded.set(false); return; }
    this.expanded.set(true);
    setTimeout(() => this.field().nativeElement.focus());
  }

  onBlur(): void { if (!this.value()) this.expanded.set(false); }

  onEscape(e: Event): void {
    e.preventDefault();
    // Keep the text (the existing search boxes never cleared on Esc); an active query stays expanded.
    if (!this.value()) this.expanded.set(false);
    (this.field().nativeElement.previousElementSibling as HTMLElement | null)?.focus();
  }
}
