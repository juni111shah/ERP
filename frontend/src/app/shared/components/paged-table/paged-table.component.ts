import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, NgZone, TemplateRef, afterEveryRender, computed, contentChild, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ClearOutline, ColumnHeightOutline, FilterFill, HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline } from '@ant-design/icons-angular/icons';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule, provideNzIconsPatch } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { PagedList } from '@core/api/paged-list';
import { I18nService } from '@core/i18n/i18n.service';
import { CsvExportService } from '@core/services/csv-export.service';
import { TABLE_DENSITIES, TableDensityService } from '@core/services/table-density.service';
import { KeyboardClickDirective } from '../../directives/keyboard-click.directive';
import { RevealDirective } from '../../directives/reveal.directive';
import { ExpandSearchComponent } from '../expand-search/expand-search.component';
import { PageChange, PaginationBarComponent } from '../pagination-bar/pagination-bar.component';
import { RowActionsComponent } from '../row-actions/row-actions.component';
import { StatusTagComponent } from '../status-tag/status-tag.component';
import { StickyScrollbar } from './sticky-scrollbar';
import { ColumnFilterKind, ColumnPin, FilterOption, TableColumn, configuredOptions, displayCell, filterKindOf, inferredAlign, isStatusColumn, readCell } from './table-column';
import { FilterState, collectSeenOptions, isFilterSet, rowMatches, serverCriteria } from './table-filters';
import { ColumnView, EMPTY_LAYOUT, TableLayout, layoutColumns, readLayout, renderedColumns, withVisibility, writeLayout } from './table-layout';

export { EMPTY_CELL } from './table-column';
export type { ColumnFilterKind, ColumnPin, TableColumn } from './table-column';

/**
 * Generic server-side table for any PagedList: search box, sortable headers, paging, empty/loading states, plus per-column
 * header filters, a Columns panel (show/hide, drag to reorder, pin to start/end) and a per-table layout saved in localStorage.
 * Toolbar actions go in <ng-content select="[toolbar]">; a custom cell template can be passed as #cell.
 */
@Component({
  selector: 'erp-paged-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyboardClickDirective, ExpandSearchComponent, FormsModule, NgTemplateOutlet, CdkDropList, CdkDrag, CdkDragHandle, NzTableModule, NzInputModule, NzIconModule, NzEmptyModule, NzButtonModule, NzCheckboxModule,
    NzDatePickerModule, NzDropDownModule, NzInputNumberModule, NzMenuModule, NzPopoverModule, NzSelectModule, NzToolTipModule, PaginationBarComponent, RevealDirective, RowActionsComponent, StatusTagComponent],
  providers: [provideNzIconsPatch([ColumnHeightOutline, FilterFill, HolderOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline, UndoOutline])],
  styleUrl: './paged-table.component.scss',
  templateUrl: './paged-table.component.html',
})
export class PagedTableComponent<T> {
  private readonly csvService = inject(CsvExportService);
  private readonly message = inject(NzMessageService);
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute, { optional: true });
  readonly density = inject(TableDensityService);
  readonly densities = TABLE_DENSITIES;

  readonly list = input.required<PagedList<T>>();
  readonly columns = input.required<TableColumn<T>[]>();
  readonly searchable = input(true);
  readonly searchPlaceholder = input('Search');
  readonly emptyText = input('Nothing here yet');
  readonly clickable = input(true);
  readonly exportable = input(true);
  /** Shows a Refresh button in the toolbar that reloads the current page; turn off when the page renders its own. */
  readonly refreshable = input(true);
  /** Horizontal scroll width for wide tables (e.g. '1400px'); null keeps the table fluid. */
  readonly scrollX = input<string | null>(null);
  readonly exportFilename = input('export');
  /** Layout storage id; defaults to the route path + tableName. */
  readonly tableId = input<string | null>(null);
  /** Distinguishes several tables on one route (used in the default tableId). */
  readonly tableName = input('table');
  /** Shows the Columns panel (show/hide, reorder, pin). */
  readonly configurable = input(true);
  /** Shows the header filters. */
  readonly filterable = input(true);
  readonly rowClick = output<T>();
  readonly cell = contentChild<TemplateRef<unknown>>('cell');
  private searchTimer?: ReturnType<typeof setTimeout>;

  // ---------- layout (order / visibility / pinning), persisted per table ----------
  private readonly routePath = (this.route?.snapshot.pathFromRoot ?? []).map((r) => r.routeConfig?.path ?? '').filter(Boolean).join('/') || 'root';
  private readonly tableKey = computed(() => this.tableId() ?? `${this.routePath}:${this.tableName()}`);
  readonly storageKey = computed(() => `erp.table.layout:${this.tableKey()}`);
  /** Remembered page size, per table. */
  readonly pageSizeKey = computed(() => `erp.table.pageSize:${this.tableKey()}`);
  readonly layout = signal<TableLayout>(EMPTY_LAYOUT);
  private readonly loadLayout = effect(() => {
    const key = this.storageKey();
    untracked(() => this.layout.set(readLayout(key)));
  });

  /** Every column in the user's order, with its pin and visibility. */
  readonly allColumns = computed((): ColumnView<T>[] => layoutColumns(this.columns(), this.layout()));
  /** Rendered columns: start-pinned first, then free, then end-pinned (sticky cells must sit at the edges). */
  readonly visibleColumns = computed(() => renderedColumns(this.allColumns()));
  readonly visibleCount = computed(() => this.allColumns().filter((c) => !c.hidden).length);
  /** Every table can scroll sideways, so its pinned columns hold when a narrow screen or a long value makes it wider than the page. */
  readonly scroll = computed(() => ({ x: this.scrollX() ?? 'max-content' }));

  /** Numbers and amounts line up at the end of the cell; everything else follows the text. */
  readonly aligns = computed((): Record<string, 'left' | 'right' | null> => {
    const out: Record<string, 'left' | 'right' | null> = {};
    const items = this.list().items();
    for (const c of this.columns()) out[c.key] = c.align ?? inferredAlign(c, items);
    return out;
  });

  setVisible(key: string, visible: boolean): void {
    if (!visible && this.visibleCount() <= 1) return;
    this.saveLayout((l) => withVisibility(l, key, visible));
  }
  setPin(key: string, pin: ColumnPin | null): void { this.saveLayout((l) => ({ ...l, pins: { ...l.pins, [key]: pin ?? 'none' } })); }
  drop(e: CdkDragDrop<unknown>): void {
    const order = this.allColumns().map((c) => c.key);
    moveItemInArray(order, e.previousIndex, e.currentIndex);
    this.saveLayout((l) => ({ ...l, order }));
  }
  resetLayout(): void {
    this.layout.set(EMPTY_LAYOUT);
    writeLayout(this.storageKey(), null);
  }
  private saveLayout(f: (l: TableLayout) => TableLayout): void {
    const next = f(this.layout());
    this.layout.set(next);
    writeLayout(this.storageKey(), next);
  }

  // ---------- column filters ----------
  readonly filters = signal<Record<string, FilterState>>({});
  readonly openKey = signal<string | null>(null);
  /** working copy edited in the open filter dropdown */
  draft: FilterState = {};
  private readonly seen = signal<Record<string, FilterOption[]>>({});

  /** filter kind per column key (null = no filter) */
  readonly kinds = computed((): Record<string, ColumnFilterKind | null> => {
    const out: Record<string, ColumnFilterKind | null> = {};
    const items = this.list().items();
    const on = this.filterable();
    for (const c of this.columns()) out[c.key] = on ? filterKindOf(c, items) : null;
    return out;
  });

  /** remembers select values seen on loaded pages, so options survive server-side filtering */
  private readonly collect = effect(() => {
    const items = this.list().items();
    const cols = this.columns();
    const kinds = this.kinds();
    untracked(() => {
      const next = collectSeenOptions(this.seen(), items, cols, kinds);
      if (next) this.seen.set(next);
    });
  });
  readonly options = computed((): Record<string, FilterOption[] | undefined> => {
    const out: Record<string, FilterOption[] | undefined> = { ...this.seen() };
    for (const c of this.columns()) {
      if (c.filterOptions) out[c.key] = configuredOptions(c);
    }
    return out;
  });

  readonly activeCount = computed(() => Object.keys(this.filters()).filter((k) => isFilterSet(this.filters()[k])).length);
  private readonly clientFilters = computed(() => {
    const f = this.filters();
    return this.columns().filter((c) => !c.filterField && isFilterSet(f[c.key]) && this.kinds()[c.key]);
  });
  readonly clientFiltered = computed(() => this.clientFilters().length > 0);
  /** loaded page minus rows rejected by client-side column filters */
  readonly rows = computed(() => {
    const items = this.list().items();
    const active = this.clientFilters();
    if (!active.length) return items;
    const f = this.filters();
    return items.filter((r) => active.every((c) => rowMatches(r, c, f[c.key], this.kinds()[c.key]!)));
  });
  readonly hasRows = computed(() => this.rows().length > 0);

  isActive(key: string): boolean { return isFilterSet(this.filters()[key]); }

  onFilterOpen(key: string, open: boolean): void {
    if (open) {
      const cur = this.filters()[key] ?? {};
      this.draft = { ...cur, values: cur.values ? [...cur.values] : [], range: cur.range ? [...cur.range] : null };
    }
    this.openKey.set(open ? key : null);
  }
  apply(key: string): void {
    const next = { ...this.filters(), [key]: { ...this.draft } };
    if (!isFilterSet(next[key])) delete next[key];
    this.setFilters(next);
    this.openKey.set(null);
  }
  clear(key: string): void {
    const next = { ...this.filters() };
    delete next[key];
    this.draft = {};
    this.setFilters(next);
    this.openKey.set(null);
  }
  clearFilters(): void { this.setFilters({}); }

  /** Sets the filters; the list asks the server again only when the server-side criteria changed. */
  private setFilters(next: Record<string, FilterState>): void {
    const prevServer = JSON.stringify(serverCriteria(this.columns(), this.kinds(), this.filters()));
    this.filters.set(next);
    const server = serverCriteria(this.columns(), this.kinds(), next);
    if (JSON.stringify(server) !== prevServer) this.list().setColumnFilters(server);
  }

  // ---------- cells ----------
  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }

  read(row: T, c: TableColumn<T>): unknown { return readCell(row, c); }

  /** What a cell shows: dates in the shared format and API values in words. Sorting, filtering and exports keep the raw value. */
  display(row: T, c: TableColumn<T>): unknown { return displayCell(row, c); }

  isStatusColumn(c: TableColumn<T>): boolean { return isStatusColumn(c); }

  // ---------- search, paging and sorting ----------
  onSearch(term: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.list().setSearch(term), 300);
  }

  onPage(e: PageChange): void {
    const q = this.list().query();
    if (e.page !== q.page || e.pageSize !== q.pageSize) this.list().setPage(e.page, e.pageSize);
  }

  /** The sort chosen in the column headers; until the user picks one, the list keeps the order the page asked for. */
  private headerSort: string | null = null;

  onQuery(p: NzTableQueryParams): void {
    const sorted = p.sort.find((s) => s.value);
    const sort = sorted ? (sorted.value === 'descend' ? '-' : '') + sorted.key : null;
    const q = this.list().query();
    if (sort !== this.headerSort) {
      this.headerSort = sort;
      this.list().setSort(sort);
    } else if (p.pageIndex !== q.page || p.pageSize !== q.pageSize) this.list().setPage(p.pageIndex, p.pageSize);
  }

  // ---------- keyboard + sticky horizontal scrollbar ----------
  private readonly grid = viewChild<ElementRef<HTMLElement>>('grid');
  private readonly hscroll = viewChild<ElementRef<HTMLElement>>('hscroll');
  private readonly pager = viewChild(PaginationBarComponent);
  private readonly scrollbar = new StickyScrollbar(inject(NgZone));
  /** body scrolls sideways (the sticky scrollbar is shown) */
  readonly overflowX = this.scrollbar.overflowX;
  readonly scrollWidth = this.scrollbar.scrollWidth;
  /** negative padding-bottom of the page's scroll container, so the sticky footer sits flush with the viewport edge */
  readonly stickyBottom = this.scrollbar.stickyBottom;
  private readonly bindScroll = afterEveryRender(() => this.scrollbar.attach(this.grid()?.nativeElement, this.hscroll()?.nativeElement ?? null));
  private readonly cleanup = inject(DestroyRef).onDestroy(() => this.scrollbar.detach());

  /** Table region keys: PageUp/PageDown and ←/→ page (mirrored in RTL), Ctrl+Home/End first/last page, Shift+←/→ scroll sideways. */
  onGridKey(e: KeyboardEvent): void {
    const target = e.target as HTMLElement;
    if (target.closest('input, textarea, select, button, a, [contenteditable], .ant-select, .ant-dropdown, .ant-picker')) return;
    if (e.shiftKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) {
      if (this.scrollbar.scrollBy(e.key === 'ArrowRight' ? 160 : -160)) e.preventDefault();
      return;
    }
    if (this.pager()?.handleKey(e, { arrows: true, homeEnd: 'ctrl' })) e.preventDefault();
  }

  /** Exports the visible columns, in the on-screen order, of the rows shown (client-side column filters applied). */
  exportCsv(): void {
    const items = this.rows();
    if (!items || items.length === 0) {
      this.message.warning(this.t('table.noExport', 'No data to export'));
      return;
    }
    const visible = this.visibleColumns().map((v) => v.col);
    const cols = visible.map((c) => ({ field: c.key, header: c.label }));
    const data = items.map((row) => {
      const obj: Record<string, unknown> = {};
      visible.forEach((c) => { obj[c.key] = readCell(row, c); });
      return obj;
    });
    this.csvService.exportToCsv(data, this.exportFilename(), cols);
    this.message.success(`${this.exportFilename()}.csv: ${items.length} ${this.t('table.rowsExported', 'rows exported')}`);
  }
}
