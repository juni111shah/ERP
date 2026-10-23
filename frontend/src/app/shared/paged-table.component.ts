import { ChangeDetectionStrategy, Component, TemplateRef, computed, contentChild, effect, inject, input, output, signal, untracked } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { ClearOutline, FilterFill, HolderOutline, UndoOutline, VerticalLeftOutline, VerticalRightOutline } from '@ant-design/icons-angular/icons';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule, provideNzIconsPatch } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { FilterCriterion } from '../core/api/api.models';
import { PagedList } from '../core/api/paged-list';
import { I18nService } from '../core/i18n/i18n.service';
import { CsvExportService } from '../core/services/csv-export.service';

export type ColumnFilterKind = 'text' | 'select' | 'date' | 'number';
export type ColumnPin = 'left' | 'right';

export interface TableColumn<T> {
  key: string;
  label: string;
  /** server sort field ([Sortable] on the entity); omit to disable sorting */
  sortField?: string;
  width?: string;
  /** 'right' for amounts */
  align?: 'left' | 'right';
  /** cell text; defaults to row[key] */
  value?: (row: T) => unknown;
  /** Header filter kind. Inferred from the key and the loaded values when omitted; `false` turns the filter off. */
  filter?: ColumnFilterKind | false;
  /**
   * Server filter field ([Filterable] on the entity). When set the filter is sent as `filter=field:op:value` (select → eq/in,
   * date → gte/lte, number → gte/lte, text → contains); otherwise the column filters the rows already loaded on the current page.
   */
  filterField?: string;
  /** Options for a select filter (raw values, optionally with labels). Defaults to the values seen in the loaded rows. */
  filterOptions?: readonly (string | { value: string; label: string })[];
  /** Raw value used for filtering; defaults to row[key], else value(row). */
  filterValue?: (row: T) => unknown;
  /** Pinned by default (users can change it; the first column is pinned to the start by default when the table scrolls sideways). */
  sticky?: ColumnPin;
  /** Hidden until the user turns it on in the Columns panel. */
  hidden?: boolean;
}

interface FilterState { text?: string; values?: string[]; range?: (Date | null)[] | null; min?: number | null; max?: number | null; }
interface Layout { order: string[]; hidden: string[]; shown: string[]; pins: Record<string, ColumnPin | 'none'>; }
interface ColumnView<T> { col: TableColumn<T>; key: string; pin: ColumnPin | null; hidden: boolean; }
interface Option { value: string; label: string; }

const EMPTY_LAYOUT: Layout = { order: [], hidden: [], shown: [], pins: {} };
const ENUM_KEY = /^(status|stage|kind|type|usage|priority|category|source|sourceType|direction|method|action|roles?|isActive|isCreditNote|currency|currencyCode|dealType|channel|entityType|recordType)$/i;
const DATE_KEY = /(date|Date|On|At|Ends)$/;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}/;

function isoDate(d: Date | null | undefined): string | null {
  if (!d) return null;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
function humanizeValue(v: string): string {
  const s = v.replace(/[._-]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim().toLowerCase();
  return s ? s[0].toUpperCase() + s.slice(1) : v;
}

/**
 * Generic server-side table for any PagedList: search box, sortable headers, paging, empty/loading states, plus per-column
 * header filters, a Columns panel (show/hide, drag to reorder, pin to start/end) and a per-table layout saved in localStorage.
 * Toolbar actions go in <ng-content select="[toolbar]">; a custom cell template can be passed as #cell.
 */
@Component({
  selector: 'erp-paged-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgTemplateOutlet, CdkDropList, CdkDrag, CdkDragHandle, NzTableModule, NzInputModule, NzIconModule, NzEmptyModule, NzButtonModule, NzCheckboxModule,
    NzDatePickerModule, NzDropDownModule, NzInputNumberModule, NzPopoverModule, NzSelectModule, NzToolTipModule],
  providers: [provideNzIconsPatch([FilterFill, HolderOutline, VerticalLeftOutline, VerticalRightOutline, ClearOutline, UndoOutline])],
  styles: [`.bar { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; flex-wrap: wrap; } .bar .search { max-width: 320px; } .spacer { flex: 1; } tr.clickable { cursor: pointer; }
    :host { display: block; min-width: 0; max-width: 100%; } td[nzAlign=right], td[style*="right"] { white-space: nowrap; }
    .note { color: #8c8c8c; font-size: 12px; }
    .flt { padding: 8px; width: 240px; display: flex; flex-direction: column; gap: 8px; } .flt .row { display: flex; gap: 8px; } .flt .row > * { flex: 1; }
    .flt .btns { display: flex; justify-content: space-between; gap: 8px; }
    .cols { min-width: 260px; max-height: 360px; overflow: auto; } .col { display: flex; align-items: center; gap: 6px; padding: 4px 2px; background: inherit; }
    .col .handle { cursor: move; color: #8c8c8c; } .col label { flex: 1; min-width: 0; } .col .on { color: #1677ff; background: rgba(22,119,255,.08); }
    .cdk-drag-preview { background: #fff; box-shadow: 0 4px 12px rgba(0,0,0,.15); display: flex; align-items: center; gap: 6px; padding: 4px 8px; border-radius: 4px; }
    .cdk-drag-placeholder { opacity: .3; } .cdk-drop-list-dragging .col:not(.cdk-drag-placeholder) { transition: transform 200ms ease; }
    .reset { margin-top: 8px; }`],
  template: `
    <div class="bar">
      @if (searchable()) {
        <nz-input-group class="search" [nzPrefix]="prefix">
          <input nz-input [placeholder]="searchPlaceholder()" [ngModel]="list().query().search ?? ''" (ngModelChange)="onSearch($event)" data-testid="table-search" />
        </nz-input-group>
        <ng-template #prefix><span nz-icon nzType="search"></span></ng-template>
      }
      @if (activeCount() > 0) {
        <button nz-button nzType="link" (click)="clearFilters()" data-testid="table-clear-filters"><span nz-icon nzType="clear"></span>{{ t('table.clearFilters', 'Clear filters') }} ({{ activeCount() }})</button>
        @if (clientFiltered()) { <span class="note" data-testid="table-filter-note">{{ t('table.pageFilterNote', 'Showing') }} {{ rows().length }} / {{ list().items().length }} {{ t('table.onThisPage', 'on this page') }}</span> }
      }
      <span class="spacer"></span>
      @if (exportable()) {
        <button nz-button nzType="default" (click)="exportCsv()" [disabled]="!hasRows()">
          <span nz-icon nzType="download"></span> {{ t('table.exportCsv', 'Export CSV') }}
        </button>
      }
      @if (configurable()) {
        <button nz-button nz-popover nzPopoverTrigger="click" [nzPopoverContent]="colPanel" [nzPopoverTitle]="t('table.columns', 'Columns')" nzPopoverPlacement="bottomRight" data-testid="table-columns">
          <span nz-icon nzType="setting"></span> {{ t('table.columns', 'Columns') }}
        </button>
      }
      <ng-content select="[toolbar]" />
    </div>
    <ng-template #colPanel>
      <div class="cols" cdkDropList (cdkDropListDropped)="drop($event)" data-testid="table-columns-panel">
        @for (v of allColumns(); track v.key) {
          <div class="col" cdkDrag cdkDragLockAxis="y" [attr.data-testid]="'col-' + v.key">
            <span class="handle" cdkDragHandle nz-icon nzType="holder" [attr.aria-label]="t('table.dragToReorder', 'Drag to reorder')"></span>
            <label nz-checkbox [ngModel]="!v.hidden" (ngModelChange)="setVisible(v.key, $event)" [nzDisabled]="!v.hidden && visibleCount() <= 1">{{ v.col.label }}</label>
            <button nz-button nzType="text" nzSize="small" [class.on]="v.pin === 'left'" (click)="setPin(v.key, v.pin === 'left' ? null : 'left')"
                    nz-tooltip [nzTooltipTitle]="t('table.pinStart', 'Pin to start')" [attr.data-testid]="'pin-left-' + v.key"><span nz-icon nzType="vertical-right"></span></button>
            <button nz-button nzType="text" nzSize="small" [class.on]="v.pin === 'right'" (click)="setPin(v.key, v.pin === 'right' ? null : 'right')"
                    nz-tooltip [nzTooltipTitle]="t('table.pinEnd', 'Pin to end')" [attr.data-testid]="'pin-right-' + v.key"><span nz-icon nzType="vertical-left"></span></button>
          </div>
        }
      </div>
      <button class="reset" nz-button nzSize="small" nzBlock (click)="resetLayout()" data-testid="table-reset-layout"><span nz-icon nzType="undo"></span>{{ t('table.resetLayout', 'Reset layout') }}</button>
    </ng-template>
    <nz-table #tbl [nzData]="rows()" [nzTotal]="list().total()" [nzPageIndex]="list().query().page" [nzPageSize]="list().query().pageSize"
              [nzFrontPagination]="false" [nzLoading]="list().loading()" nzShowSizeChanger [nzPageSizeOptions]="[10, 25, 50, 100]"
              nzSize="middle" [nzScroll]="scroll()" [nzNoResult]="empty" (nzQueryParams)="onQuery($event)" data-testid="paged-table">
      <thead>
        <tr>
          @for (v of visibleColumns(); track v.key) {
            <th [nzColumnKey]="v.col.sortField ?? v.key" [nzShowSort]="!!v.col.sortField" [nzSortFn]="!!v.col.sortField" [nzWidth]="v.col.width ?? null" [nzAlign]="v.col.align ?? null"
                [nzLeft]="v.pin === 'left'" [nzRight]="v.pin === 'right'" [nzCustomFilter]="!!kinds()[v.key]">
              {{ v.col.label }}
              @if (kinds()[v.key]; as kind) {
                <ng-container ngProjectAs="nz-filter-trigger">
                <nz-filter-trigger [nzVisible]="openKey() === v.key" (nzVisibleChange)="onFilterOpen(v.key, $event)" [nzActive]="isActive(v.key)" [nzDropdownMenu]="menu" [attr.data-testid]="'filter-' + v.key">
                  <span nz-icon nzType="filter" nzTheme="fill"></span>
                </nz-filter-trigger>
                <nz-dropdown-menu #menu="nzDropdownMenu">
                  <div class="flt ant-table-filter-dropdown" (click)="$event.stopPropagation()" [attr.data-testid]="'filter-panel-' + v.key">
                    @switch (kind) {
                      @case ('text') { <input nz-input [(ngModel)]="draft.text" [placeholder]="t('table.contains', 'Contains…')" (keyup.enter)="apply(v.key)" /> }
                      @case ('select') {
                        <nz-select nzMode="multiple" [(ngModel)]="draft.values" [nzPlaceHolder]="t('table.any', 'Any')" nzAllowClear style="width:100%">
                          @for (o of options()[v.key] ?? []; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }
                        </nz-select>
                      }
                      @case ('date') { <nz-range-picker [(ngModel)]="draft.range" style="width:100%"></nz-range-picker> }
                      @case ('number') {
                        <div class="row">
                          <nz-input-number [(ngModel)]="draft.min" [nzPlaceHolder]="t('table.min', 'Min')"></nz-input-number>
                          <nz-input-number [(ngModel)]="draft.max" [nzPlaceHolder]="t('table.max', 'Max')"></nz-input-number>
                        </div>
                      }
                    }
                    <div class="btns">
                      <button nz-button nzSize="small" (click)="clear(v.key)">{{ t('table.reset', 'Reset') }}</button>
                      <button nz-button nzSize="small" nzType="primary" (click)="apply(v.key)" [attr.data-testid]="'filter-apply-' + v.key">{{ t('table.apply', 'Apply') }}</button>
                    </div>
                  </div>
                </nz-dropdown-menu>
                </ng-container>
              }
            </th>
          }
        </tr>
      </thead>
      <tbody>
        @for (row of tbl.data; track $index) {
          <tr [class.clickable]="clickable()" (click)="rowClick.emit(row)" [attr.data-testid]="'row-' + $index">
            @for (v of visibleColumns(); track v.key) {
              <td [nzAlign]="v.col.align ?? null" [nzLeft]="v.pin === 'left'" [nzRight]="v.pin === 'right'">
                @if (cell(); as tpl) { <ng-container *ngTemplateOutlet="tpl; context: { $implicit: row, column: v.col, value: read(row, v.col) }" /> }
                @else { {{ read(row, v.col) }} }
              </td>
            }
          </tr>
        }
      </tbody>
    </nz-table>
    <ng-template #empty><nz-empty [nzNotFoundContent]="emptyText()"></nz-empty></ng-template>`,
})
export class PagedTableComponent<T> {
  private readonly csvService = inject(CsvExportService);
  private readonly message = inject(NzMessageService);
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute, { optional: true });

  readonly list = input.required<PagedList<T>>();
  readonly columns = input.required<TableColumn<T>[]>();
  readonly searchable = input(true);
  readonly searchPlaceholder = input('Search');
  readonly emptyText = input('Nothing here yet');
  readonly clickable = input(true);
  readonly exportable = input(true);
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
  readonly storageKey = computed(() => `erp.table.layout:${this.tableId() ?? `${this.routePath}:${this.tableName()}`}`);
  readonly layout = signal<Layout>(EMPTY_LAYOUT);
  private readonly loadLayout = effect(() => {
    const key = this.storageKey();
    untracked(() => this.layout.set(this.readLayout(key)));
  });

  /** Every column in the user's order, with its pin and visibility. */
  readonly allColumns = computed((): ColumnView<T>[] => {
    const cols = this.columns();
    const l = this.layout();
    const keys = cols.map((c) => c.key);
    const order = l.order.filter((k) => keys.includes(k));
    keys.forEach((k, i) => {
      if (order.includes(k)) return;
      const prev = keys.slice(0, i).reverse().find((p) => order.includes(p));
      order.splice(prev ? order.indexOf(prev) + 1 : 0, 0, k);
    });
    const byKey = new Map(cols.map((c) => [c.key, c]));
    return order.map((k) => {
      const col = byKey.get(k)!;
      const saved = l.pins[k];
      const pin: ColumnPin | null = saved === 'none' ? null : saved ?? this.defaultPin(col, keys.indexOf(k));
      const hidden = l.hidden.includes(k) || (!!col.hidden && !l.shown.includes(k));
      return { col, key: k, pin, hidden };
    });
  });
  /** Rendered columns: start-pinned first, then free, then end-pinned (sticky cells must sit at the edges). */
  readonly visibleColumns = computed(() => {
    let v = this.allColumns().filter((c) => !c.hidden);
    if (!v.length) v = this.allColumns().slice(0, 1);
    return [...v.filter((c) => c.pin === 'left'), ...v.filter((c) => !c.pin), ...v.filter((c) => c.pin === 'right')];
  });
  readonly visibleCount = computed(() => this.allColumns().filter((c) => !c.hidden).length);
  readonly scroll = computed(() => {
    const x = this.scrollX();
    if (x) return { x };
    return this.visibleColumns().some((c) => c.pin) ? { x: 'max-content' } : {};
  });

  private defaultPin(c: TableColumn<T>, index: number): ColumnPin | null {
    if (c.sticky) return c.sticky;
    if (c.key === 'actions') return 'right';
    return index === 0 && !!this.scrollX() ? 'left' : null;
  }

  setVisible(key: string, visible: boolean): void {
    if (!visible && this.visibleCount() <= 1) return;
    this.saveLayout((l) => ({
      ...l,
      hidden: visible ? l.hidden.filter((k) => k !== key) : [...l.hidden.filter((k) => k !== key), key],
      shown: visible ? [...l.shown.filter((k) => k !== key), key] : l.shown.filter((k) => k !== key),
    }));
  }
  setPin(key: string, pin: ColumnPin | null): void { this.saveLayout((l) => ({ ...l, pins: { ...l.pins, [key]: pin ?? 'none' } })); }
  drop(e: CdkDragDrop<unknown>): void {
    const order = this.allColumns().map((c) => c.key);
    moveItemInArray(order, e.previousIndex, e.currentIndex);
    this.saveLayout((l) => ({ ...l, order }));
  }
  resetLayout(): void {
    this.layout.set(EMPTY_LAYOUT);
    try { localStorage.removeItem(this.storageKey()); } catch { /* storage unavailable */ }
  }
  private saveLayout(f: (l: Layout) => Layout): void {
    const next = f(this.layout());
    this.layout.set(next);
    try { localStorage.setItem(this.storageKey(), JSON.stringify(next)); } catch { /* storage unavailable */ }
  }
  private readLayout(key: string): Layout {
    try {
      const raw = localStorage.getItem(key);
      if (!raw) return EMPTY_LAYOUT;
      const p = JSON.parse(raw) as Partial<Layout>;
      const arr = (a: unknown) => (Array.isArray(a) ? a.filter((x): x is string => typeof x === 'string') : []);
      return { order: arr(p.order), hidden: arr(p.hidden), shown: arr(p.shown), pins: p.pins && typeof p.pins === 'object' ? p.pins : {} };
    } catch { return EMPTY_LAYOUT; }
  }

  // ---------- column filters ----------
  readonly filters = signal<Record<string, FilterState>>({});
  readonly openKey = signal<string | null>(null);
  /** working copy edited in the open filter dropdown */
  draft: FilterState = {};
  private readonly seen = signal<Record<string, Option[]>>({});

  /** filter kind per column key (null = no filter) */
  readonly kinds = computed((): Record<string, ColumnFilterKind | null> => {
    const out: Record<string, ColumnFilterKind | null> = {};
    const items = this.list().items();
    const on = this.filterable();
    for (const c of this.columns()) out[c.key] = on ? this.kindOf(c, items) : null;
    return out;
  });

  private kindOf(c: TableColumn<T>, items: T[]): ColumnFilterKind | null {
    if (c.filter === false || c.key === 'actions') return null;
    if (c.filter) return c.filter;
    if (c.filterOptions) return 'select';
    if (ENUM_KEY.test(c.key)) return 'select';
    if (DATE_KEY.test(c.key)) return 'date';
    const sample = items.map((r) => this.raw(r, c)).find((v) => v !== null && v !== undefined && v !== '');
    if (typeof sample === 'boolean') return 'select';
    if (typeof sample === 'number') return 'number';
    if (typeof sample === 'string' && ISO_DATE.test(sample)) return 'date';
    if (c.align === 'right') return 'number';
    return 'text';
  }

  /** remembers select values seen on loaded pages, so options survive server-side filtering */
  private readonly collect = effect(() => {
    const items = this.list().items();
    const cols = this.columns();
    const kinds = this.kinds();
    untracked(() => {
      const next = { ...this.seen() };
      let changed = false;
      for (const c of cols) {
        if (kinds[c.key] !== 'select' || c.filterOptions) continue;
        const known = new Map((next[c.key] ?? []).map((o) => [o.value, o]));
        const before = known.size;
        for (const r of items) {
          const raw = this.raw(r, c);
          const vals = Array.isArray(raw) ? raw : [raw];
          for (const v of vals) {
            if (v === null || v === undefined || v === '') continue;
            const value = String(v);
            if (known.has(value)) continue;
            const shown = Array.isArray(raw) ? value : this.read(r, c);
            const label = typeof v === 'boolean' ? (v ? 'Yes' : 'No') : shown !== '' && String(shown) !== value ? String(shown) : humanizeValue(value);
            known.set(value, { value, label });
          }
        }
        if (known.size !== before) { next[c.key] = [...known.values()].sort((a, b) => a.label.localeCompare(b.label)); changed = true; }
      }
      if (changed) this.seen.set(next);
    });
  });
  readonly options = computed((): Record<string, Option[] | undefined> => {
    const out: Record<string, Option[] | undefined> = { ...this.seen() };
    for (const c of this.columns()) {
      if (c.filterOptions) out[c.key] = c.filterOptions.map((o) => (typeof o === 'string' ? { value: o, label: humanizeValue(o) } : o));
    }
    return out;
  });

  readonly activeCount = computed(() => Object.keys(this.filters()).filter((k) => this.isSet(this.filters()[k])).length);
  private readonly clientFilters = computed(() => {
    const f = this.filters();
    return this.columns().filter((c) => !c.filterField && this.isSet(f[c.key]) && this.kinds()[c.key]);
  });
  readonly clientFiltered = computed(() => this.clientFilters().length > 0);
  /** loaded page minus rows rejected by client-side column filters */
  readonly rows = computed(() => {
    const items = this.list().items();
    const active = this.clientFilters();
    if (!active.length) return items;
    const f = this.filters();
    return items.filter((r) => active.every((c) => this.matches(r, c, f[c.key], this.kinds()[c.key]!)));
  });
  readonly hasRows = computed(() => this.rows().length > 0);

  isActive(key: string): boolean { return this.isSet(this.filters()[key]); }
  private isSet(f: FilterState | undefined): boolean {
    if (!f) return false;
    return !!f.text?.trim() || !!f.values?.length || !!(f.range && (f.range[0] || f.range[1])) || f.min != null || f.max != null;
  }

  onFilterOpen(key: string, open: boolean): void {
    if (open) {
      const cur = this.filters()[key] ?? {};
      this.draft = { ...cur, values: cur.values ? [...cur.values] : [], range: cur.range ? [...cur.range] : null };
    }
    this.openKey.set(open ? key : null);
  }
  apply(key: string): void {
    const next = { ...this.filters(), [key]: { ...this.draft } };
    if (!this.isSet(next[key])) delete next[key];
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

  private setFilters(next: Record<string, FilterState>): void {
    const prevServer = JSON.stringify(this.serverCriteria(this.filters()));
    this.filters.set(next);
    const server = this.serverCriteria(next);
    if (JSON.stringify(server) !== prevServer) this.list().setColumnFilters(server);
  }

  private serverCriteria(f: Record<string, FilterState>): FilterCriterion[] {
    const out: FilterCriterion[] = [];
    for (const c of this.columns()) {
      const s = f[c.key];
      const field = c.filterField;
      if (!field || !this.isSet(s)) continue;
      switch (this.kinds()[c.key]) {
        case 'text': out.push({ field, op: 'contains', value: s.text!.trim() }); break;
        case 'select': out.push(s.values!.length === 1 ? { field, op: 'eq', value: s.values![0] } : { field, op: 'in', value: s.values! }); break;
        case 'date': {
          const from = isoDate(s.range?.[0]); const to = isoDate(s.range?.[1]);
          if (from) out.push({ field, op: 'gte', value: from });
          if (to) out.push({ field, op: 'lte', value: to });
          break;
        }
        case 'number':
          if (s.min != null) out.push({ field, op: 'gte', value: s.min });
          if (s.max != null) out.push({ field, op: 'lte', value: s.max });
          break;
      }
    }
    return out;
  }

  private matches(row: T, c: TableColumn<T>, f: FilterState, kind: ColumnFilterKind): boolean {
    const raw = this.raw(row, c);
    switch (kind) {
      case 'text': {
        const q = f.text!.trim().toLowerCase();
        return String(this.read(row, c)).toLowerCase().includes(q) || (raw != null && typeof raw !== 'object' && String(raw).toLowerCase().includes(q));
      }
      case 'select': {
        const vals = (Array.isArray(raw) ? raw : [raw]).filter((v) => v !== null && v !== undefined).map((v) => String(v));
        return !f.values?.length || vals.some((v) => f.values!.includes(v));
      }
      case 'date': {
        const d = raw == null ? '' : raw instanceof Date ? isoDate(raw) ?? '' : String(raw).slice(0, 10);
        const from = isoDate(f.range?.[0]); const to = isoDate(f.range?.[1]);
        if (!d) return !from && !to;
        return (!from || d >= from) && (!to || d <= to);
      }
      case 'number': {
        const n = typeof raw === 'number' ? raw : parseFloat(String(this.read(row, c)).replace(/[^0-9.\-]/g, ''));
        if (Number.isNaN(n)) return f.min == null && f.max == null;
        return (f.min == null || n >= f.min) && (f.max == null || n <= f.max);
      }
    }
  }

  private raw(row: T, c: TableColumn<T>): unknown {
    if (c.filterValue) return c.filterValue(row);
    const v = (row as Record<string, unknown>)[c.key];
    return v !== undefined ? v : c.value ? c.value(row) : undefined;
  }

  // ---------- existing behaviour ----------
  t(key: string, fallback: string): string { return this.i18n.t(key, fallback); }

  read(row: T, c: TableColumn<T>): unknown {
    const v = c.value ? c.value(row) : (row as Record<string, unknown>)[c.key];
    return Array.isArray(v) ? v.join(', ') : typeof v === 'boolean' ? (v ? 'Yes' : 'No') : (v ?? '');
  }

  onSearch(term: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.list().setSearch(term), 300);
  }

  onQuery(p: NzTableQueryParams): void {
    const sorted = p.sort.find((s) => s.value);
    const sort = sorted ? (sorted.value === 'descend' ? '-' : '') + sorted.key : null;
    const q = this.list().query();
    if ((q.sort ?? null) !== sort) this.list().setSort(sort);
    else if (p.pageIndex !== q.page || p.pageSize !== q.pageSize) this.list().setPage(p.pageIndex, p.pageSize);
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
      visible.forEach((c) => { obj[c.key] = this.read(row, c); });
      return obj;
    });
    this.csvService.exportToCsv(data as Record<string, any>[], this.exportFilename(), cols);
    this.message.success(this.t('table.exported', 'CSV exported successfully'));
  }
}
