import { ChangeDetectionStrategy, Component, ElementRef, WritableSignal, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { I18nService } from '../core/i18n/i18n.service';

export interface PageChange { page: number; pageSize: number; }
type Slot = number | 'prev5' | 'next5';

export const DEFAULT_PAGE_SIZES: readonly number[] = [10, 20, 50, 100];

/** Reads a remembered page size (erp.table.pageSize:<id>); null when none or storage is unavailable. */
export function readPageSize(key: string | null): number | null {
  if (!key) return null;
  try {
    const n = Number(localStorage.getItem(key));
    return Number.isInteger(n) && n > 0 && n <= 500 ? n : null;
  } catch { return null; }
}
function writePageSize(key: string | null, size: number): void {
  if (!key) return;
  try { localStorage.setItem(key, String(size)); } catch { /* storage unavailable */ }
}

/** Page state for a table paged in the browser (nz-table front pagination with its own pager hidden). */
export class ClientPager {
  readonly page = signal(1);
  readonly size: WritableSignal<number>;
  constructor(size = 10) { this.size = signal(size); }
  change(e: PageChange): void { this.size.set(e.pageSize); this.page.set(e.page); }
}

/**
 * Shared pagination bar: "Showing 21–30 of 245", first / previous / numbered pages with ellipsis / next / last, jump to page,
 * page-size selector (remembered per table when storageKey is set), keyboard paging (arrow keys, Home/End, PageUp/PageDown;
 * mirrored in RTL), disabled while loading, and a compact layout in narrow containers. Works for server or client paging:
 * it only reports the requested page and size through (pageChange).
 */
@Component({
  selector: 'erp-pagination-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzIconModule, NzSelectModule],
  host: { class: 'erp-pagination-bar', '[class.is-hidden]': 'hidden()' },
  styles: [`
    :host { display: block; container-type: inline-size; } :host(.is-hidden) { display: none; }
    .pg { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; padding: 8px 0; font-size: 13px; color: var(--erp-color-text-primary, rgba(0,0,0,.88)); }
    .pg.busy { opacity: .6; }
    .range { color: var(--erp-color-text-muted, #8c8c8c); font-variant-numeric: tabular-nums; white-space: nowrap; }
    .range b { color: var(--erp-color-text-primary, rgba(0,0,0,.88)); font-weight: 600; }
    .spacer { flex: 1; }
    .nav { display: flex; align-items: center; gap: 4px; }
    .pb { min-width: 32px; height: 32px; padding: 0 6px; border: 1px solid var(--erp-color-border-default, #d9d9d9); border-radius: 6px; background: var(--erp-color-bg-surface, #fff);
      color: inherit; font: inherit; font-variant-numeric: tabular-nums; cursor: pointer; display: inline-flex; align-items: center; justify-content: center;
      transition: border-color .15s, color .15s, background-color .15s, transform .15s; }
    .pb:hover:not(:disabled) { border-color: var(--erp-color-primary, #1677ff); color: var(--erp-color-primary, #1677ff); }
    .pb:focus-visible { outline: 2px solid var(--erp-color-focus-ring, rgba(22,119,255,.4)); outline-offset: 1px; }
    .pb:active:not(:disabled) { transform: scale(.94); }
    .pb:disabled { cursor: not-allowed; opacity: .4; }
    .pb.on { background: var(--erp-color-primary, #1677ff); border-color: var(--erp-color-primary, #1677ff); color: var(--erp-color-on-primary, #fff); font-weight: 600; }
    .pb.more { border-color: transparent; background: transparent; letter-spacing: 1px; }
    .pb.more .alt { display: none; } .pb.more:hover:not(:disabled) .dots, .pb.more:focus-visible .dots { display: none; } .pb.more:hover:not(:disabled) .alt, .pb.more:focus-visible .alt { display: inline-flex; }
    .cur { display: none; padding: 0 6px; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .tools { display: flex; align-items: center; gap: 8px; white-space: nowrap; }
    .jump { width: 64px; height: 32px; padding: 0 8px; border: 1px solid var(--erp-color-border-default, #d9d9d9); border-radius: 6px; background: var(--erp-color-bg-surface, #fff); color: inherit; font: inherit; text-align: center; }
    .jump:focus { outline: none; border-color: var(--erp-color-primary, #1677ff); box-shadow: 0 0 0 2px var(--erp-color-focus-ring, rgba(22,119,255,.2)); }
    .lbl { color: var(--erp-color-text-muted, #8c8c8c); }
    .size { width: 116px; }
    :host-context([dir=rtl]) .flip { transform: scaleX(-1); }
    @container (max-width: 720px) { .num, .more, .lbl-go { display: none; } .cur { display: inline; } .jump { width: 56px; } }
    @container (max-width: 520px) { .pg { gap: 8px; } .range { width: 100%; order: -1; } .spacer { display: none; } .edge { display: none; } .jumpbox { display: none; } }
    @media (prefers-reduced-motion: reduce) { .pb { transition: none; } .pb:active:not(:disabled) { transform: none; } }
  `],
  template: `
    <nav class="pg" [class.busy]="loading()" [attr.aria-label]="t('pager.label', 'Pagination')" [attr.aria-busy]="loading()" (keydown)="onKey($event)" data-testid="pagination-bar">
      <span class="range" aria-live="polite" data-testid="pager-range">
        @if (total() > 0) { {{ t('pager.showing', 'Showing') }} <b>{{ n(from()) }}–{{ n(to()) }}</b> {{ t('pager.of', 'of') }} <b>{{ n(total()) }}</b> }
        @else { {{ t('pager.noItems', 'No items') }} }
      </span>
      <span class="spacer"></span>
      <div class="nav">
        <button type="button" class="pb edge" (click)="go(1)" [disabled]="busy() || cur() <= 1" [attr.aria-label]="t('pager.first', 'First page')" [title]="t('pager.first', 'First page')" data-testid="pager-first">
          <span class="flip" nz-icon nzType="double-left"></span></button>
        <button type="button" class="pb" (click)="go(cur() - 1)" [disabled]="busy() || cur() <= 1" [attr.aria-label]="t('pager.prev', 'Previous page')" [title]="t('pager.prev', 'Previous page')" data-testid="pager-prev">
          <span class="flip" nz-icon nzType="left"></span></button>
        @for (s of slots(); track $index) {
          @if (s === 'prev5' || s === 'next5') {
            <button type="button" class="pb more" (click)="go(s === 'prev5' ? cur() - 5 : cur() + 5)" [disabled]="busy()"
                    [attr.aria-label]="s === 'prev5' ? t('pager.prev5', 'Previous 5 pages') : t('pager.next5', 'Next 5 pages')" [title]="s === 'prev5' ? t('pager.prev5', 'Previous 5 pages') : t('pager.next5', 'Next 5 pages')">
              <span class="dots" aria-hidden="true">•••</span><span class="alt flip" nz-icon [nzType]="s === 'prev5' ? 'double-left' : 'double-right'"></span></button>
          } @else {
            <button type="button" class="pb num" [class.on]="s === cur()" (click)="go(s)" [disabled]="busy()" [attr.aria-current]="s === cur() ? 'page' : null"
                    [attr.aria-label]="t('pager.page', 'Page') + ' ' + s" [attr.data-testid]="'pager-page-' + s">{{ n(s) }}</button>
          }
        }
        <span class="cur" data-testid="pager-current">{{ t('pager.page', 'Page') }} {{ n(cur()) }} / {{ n(pageCount()) }}</span>
        <button type="button" class="pb" (click)="go(cur() + 1)" [disabled]="busy() || cur() >= pageCount()" [attr.aria-label]="t('pager.next', 'Next page')" [title]="t('pager.next', 'Next page')" data-testid="pager-next">
          <span class="flip" nz-icon nzType="right"></span></button>
        <button type="button" class="pb edge" (click)="go(pageCount())" [disabled]="busy() || cur() >= pageCount()" [attr.aria-label]="t('pager.last', 'Last page')" [title]="t('pager.last', 'Last page')" data-testid="pager-last">
          <span class="flip" nz-icon nzType="double-right"></span></button>
      </div>
      <div class="tools">
        @if (showJumper() && pageCount() > 1) {
          <label class="jumpbox"><span class="lbl lbl-go">{{ t('pager.goTo', 'Go to') }}</span>
            <input class="jump" type="number" inputmode="numeric" min="1" [max]="pageCount()" [disabled]="busy()" [(ngModel)]="jumpTo" (keydown.enter)="jump()" (blur)="jump()"
                   [placeholder]="'' + cur()" [attr.aria-label]="t('pager.goToPage', 'Go to page')" data-testid="pager-jump" /></label>
        }
        @if (showSizeChanger()) {
          <nz-select class="size" nzSize="default" [ngModel]="pageSize()" (ngModelChange)="setSize($event)" [nzDisabled]="busy()" [attr.aria-label]="t('pager.rowsPerPage', 'Rows per page')" data-testid="pager-size">
            @for (o of sizes(); track o) { <nz-option [nzValue]="o" [nzLabel]="o + ' ' + t('pager.perPage', '/ page')"></nz-option> }
          </nz-select>
        }
      </div>
    </nav>`,
})
export class PaginationBarComponent {
  private readonly i18n = inject(I18nService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly total = input(0);
  /** 1-based */
  readonly page = input(1);
  readonly pageSize = input(10);
  readonly pageSizeOptions = input<readonly number[]>(DEFAULT_PAGE_SIZES);
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly showSizeChanger = input(true);
  readonly showJumper = input(true);
  /** Hide the bar when everything fits on the smallest page size (small client-side tables). */
  readonly hideOnSinglePage = input(false);
  /** localStorage key for the remembered page size, e.g. erp.table.pageSize:<table id>. */
  readonly storageKey = input<string | null>(null);
  readonly pageChange = output<PageChange>();

  jumpTo: number | null = null;

  readonly busy = computed(() => this.loading() || this.disabled());
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / Math.max(1, this.pageSize()))));
  readonly cur = computed(() => Math.min(Math.max(1, this.page()), this.pageCount()));
  readonly from = computed(() => (this.total() ? (this.cur() - 1) * this.pageSize() + 1 : 0));
  readonly to = computed(() => Math.min(this.total(), this.cur() * this.pageSize()));
  readonly sizes = computed(() => [...new Set([...this.pageSizeOptions(), this.pageSize()])].sort((a, b) => a - b));
  readonly hidden = computed(() => this.hideOnSinglePage() && this.total() <= Math.min(...this.sizes()));
  /** 1 … 4 5 [6] 7 8 … 25 — at most 7 numbered slots, the ellipses jump 5 pages. */
  readonly slots = computed((): Slot[] => {
    const count = this.pageCount(); const c = this.cur();
    if (count <= 7) return Array.from({ length: count }, (_, i) => i + 1);
    const start = Math.max(2, Math.min(c - 2, count - 5));
    const end = Math.min(count - 1, Math.max(c + 2, 6));
    const out: Slot[] = [1];
    if (start > 2) out.push('prev5');
    for (let p = start; p <= end; p++) out.push(p);
    if (end < count - 1) out.push('next5');
    out.push(count);
    return out;
  });

  /** Apply a remembered page size once, after the inputs are known. */
  private restored = false;
  private readonly restore = effect(() => {
    const key = this.storageKey();
    if (this.restored || !key) return;
    this.restored = true;
    const saved = readPageSize(key);
    const size = untracked(() => this.pageSize());
    if (saved && saved !== size) queueMicrotask(() => this.pageChange.emit({ page: 1, pageSize: saved }));
  });
  /** Step back when the current page no longer exists (rows deleted, filter narrowed). */
  private readonly clamp = effect(() => {
    const page = this.page(); const count = this.pageCount(); const total = this.total(); const loading = this.loading();
    if (!loading && total > 0 && page > count) queueMicrotask(() => this.pageChange.emit({ page: count, pageSize: untracked(() => this.pageSize()) }));
  });

  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }
  n(v: number): string { return new Intl.NumberFormat('en-US').format(v); }

  go(page: number): void {
    if (this.busy()) return;
    const p = Math.min(Math.max(1, Math.round(page)), this.pageCount());
    if (p !== this.page()) this.pageChange.emit({ page: p, pageSize: this.pageSize() });
  }
  first(): void { this.go(1); }
  prev(): void { this.go(this.cur() - 1); }
  next(): void { this.go(this.cur() + 1); }
  last(): void { this.go(this.pageCount()); }

  jump(): void {
    const v = this.jumpTo;
    this.jumpTo = null;
    if (v != null && !Number.isNaN(Number(v))) this.go(Number(v));
  }

  setSize(size: number): void {
    if (!size || size === this.pageSize()) return;
    writePageSize(this.storageKey(), size);
    // keep the first visible row on screen
    const firstRow = (this.cur() - 1) * this.pageSize();
    this.pageChange.emit({ page: Math.floor(firstRow / size) + 1, pageSize: size });
  }

  /** Arrow keys (mirrored in RTL), Home/End and PageUp/PageDown page the list; inputs and the size select keep their own keys. */
  onKey(e: KeyboardEvent): void {
    const target = e.target as HTMLElement;
    if (target.closest('input, nz-select, .ant-select')) return;
    if (this.handleKey(e)) e.preventDefault();
  }

  /** Shared with the table: returns true when the key paged the list. */
  handleKey(e: KeyboardEvent, opts: { arrows?: boolean; homeEnd?: 'plain' | 'ctrl' } = { arrows: true, homeEnd: 'plain' }): boolean {
    const rtl = this.i18n.dir() === 'rtl' || this.host.nativeElement.closest('[dir=rtl]') !== null;
    const home = opts.homeEnd === 'ctrl' ? e.ctrlKey || e.metaKey : !e.ctrlKey && !e.metaKey;
    switch (e.key) {
      case 'PageDown': this.next(); return true;
      case 'PageUp': this.prev(); return true;
      case 'ArrowRight': if (!opts.arrows || e.shiftKey || e.altKey) return false; rtl ? this.prev() : this.next(); return true;
      case 'ArrowLeft': if (!opts.arrows || e.shiftKey || e.altKey) return false; rtl ? this.next() : this.prev(); return true;
      case 'Home': if (!home) return false; this.first(); return true;
      case 'End': if (!home) return false; this.last(); return true;
    }
    return false;
  }
}
