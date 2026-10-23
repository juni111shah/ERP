import { ChangeDetectionStrategy, Component, inject, input, model, output } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { I18nService } from '../core/i18n/i18n.service';
import { ExpandSearchComponent } from './expand-search.component';
import { RevealDirective } from './reveal.directive';

/** True when every word of `q` appears (case-insensitive) in one of the values; an empty query matches everything. */
export function matchText(q: string | null | undefined, ...values: unknown[]): boolean {
  const words = (q ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) return true;
  const hay = values.filter((v) => v !== null && v !== undefined).map((v) => String(v).toLowerCase()).join(' ');
  return words.every((w) => hay.includes(w));
}

/**
 * Toolbar for tables that are not an <erp-paged-table>: an optional search box, projected filters, and a Refresh button styled like the
 * paged-table toolbar (icon first, label revealed on hover / keyboard focus through erpReveal). Projected `nz-select`s with nzAllowClear
 * collapse to a filter icon the same way; put trailing actions in an element marked `end`.
 */
@Component({
  selector: 'erp-table-tools',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ExpandSearchComponent, NzButtonModule, NzIconModule, RevealDirective],
  styles: [`.bar { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; } .bar .search { max-width: 320px; } .spacer { flex: 1; } :host { display: block; }`],
  template: `
    <div class="bar" erpReveal>
      @if (searchable()) {
        <erp-expand-search class="search" [placeholder]="placeholder()" [label]="placeholder()" [(value)]="search" [testId]="testId() + '-search'" />
      }
      <ng-content />
      <span class="spacer"></span>
      <ng-content select="[end]" />
      <button nz-button nzType="default" type="button" (click)="refresh.emit()" [disabled]="loading()" [attr.data-testid]="testId() + '-refresh'">
        <span nz-icon nzType="reload"></span> {{ t('ui.common.refresh', 'Refresh') }}
      </button>
    </div>`,
})
export class TableToolsComponent {
  private readonly i18n = inject(I18nService);
  /** Client-side search text (two-way: `[(search)]`). */
  readonly search = model('');
  readonly searchable = input(true);
  readonly placeholder = input('');
  readonly loading = input(false);
  readonly testId = input('table');
  readonly refresh = output<void>();
  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }
}
