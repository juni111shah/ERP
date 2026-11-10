import { ChangeDetectionStrategy, Component, ElementRef, WritableSignal, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { I18nService } from '@core/i18n/i18n.service';

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
  styleUrl: './pagination-bar.component.scss',
  templateUrl: './pagination-bar.component.html',
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
