import { ChangeDetectionStrategy, Component, TemplateRef, computed, contentChild, input, output, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PagedList } from '../core/api/paged-list';
import { CsvExportService } from '../core/services/csv-export.service';

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
}

/**
 * Generic server-side table for any PagedList: search box, sortable headers, paging, empty/loading states.
 * Toolbar actions go in <ng-content select="[toolbar]">; a custom cell template can be passed as #cell.
 */
@Component({
  selector: 'erp-paged-table',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgTemplateOutlet, NzTableModule, NzInputModule, NzIconModule, NzEmptyModule, NzButtonModule],
  styles: [`.bar { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; } .bar .search { max-width: 320px; } .spacer { flex: 1; } tr.clickable { cursor: pointer; }`],
  template: `
    <div class="bar">
      @if (searchable()) {
        <nz-input-group class="search" [nzPrefix]="prefix">
          <input nz-input [placeholder]="searchPlaceholder()" [ngModel]="list().query().search ?? ''" (ngModelChange)="onSearch($event)" data-testid="table-search" />
        </nz-input-group>
        <ng-template #prefix><span nz-icon nzType="search"></span></ng-template>
      }
      <span class="spacer"></span>
      @if (exportable()) {
        <button nz-button nzType="default" (click)="exportCsv()" [disabled]="!hasRows()">
          <span nz-icon nzType="download"></span> Export CSV
        </button>
      }
      <ng-content select="[toolbar]" />
    </div>
    <nz-table #t [nzData]="list().items()" [nzTotal]="list().total()" [nzPageIndex]="list().query().page" [nzPageSize]="list().query().pageSize"
              [nzFrontPagination]="false" [nzLoading]="list().loading()" nzShowSizeChanger [nzPageSizeOptions]="[10, 25, 50, 100]"
              nzSize="middle" [nzNoResult]="empty" (nzQueryParams)="onQuery($event)" data-testid="paged-table">
      <thead>
        <tr>
          @for (c of columns(); track c.key) {
            <th [nzColumnKey]="c.sortField ?? c.key" [nzShowSort]="!!c.sortField" [nzSortFn]="!!c.sortField" [nzWidth]="c.width ?? null" [nzAlign]="c.align ?? null">{{ c.label }}</th>
          }
        </tr>
      </thead>
      <tbody>
        @for (row of t.data; track $index) {
          <tr [class.clickable]="clickable()" (click)="rowClick.emit(row)" [attr.data-testid]="'row-' + $index">
            @for (c of columns(); track c.key) {
              <td [nzAlign]="c.align ?? null">
                @if (cell(); as tpl) { <ng-container *ngTemplateOutlet="tpl; context: { $implicit: row, column: c, value: read(row, c) }" /> }
                @else { {{ read(row, c) }} }
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
  
  readonly list = input.required<PagedList<T>>();
  readonly columns = input.required<TableColumn<T>[]>();
  readonly searchable = input(true);
  readonly searchPlaceholder = input('Search');
  readonly emptyText = input('Nothing here yet');
  readonly clickable = input(true);
  readonly exportable = input(true);
  readonly exportFilename = input('export');
  readonly rowClick = output<T>();
  readonly cell = contentChild<TemplateRef<unknown>>('cell');
  private searchTimer?: ReturnType<typeof setTimeout>;
  readonly hasRows = computed(() => this.list().items().length > 0);

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

  exportCsv(): void {
    const items = this.list().items();
    if (!items || items.length === 0) {
      this.message.warning('No data to export');
      return;
    }

    const cols = this.columns().map(c => ({
      field: c.key,
      header: c.label
    }));

    const data = items.map(row => {
      const obj: Record<string, any> = {};
      this.columns().forEach(c => {
        obj[c.key] = this.read(row, c);
      });
      return obj;
    });

    this.csvService.exportToCsv(data, this.exportFilename(), cols);
    this.message.success('CSV exported successfully');
  }
}
