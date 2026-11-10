import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, map, of } from 'rxjs';
import { NzInputModule } from 'ng-zorro-antd/input';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ItemsApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { MovementRow, MovementsApi, WarehousesApi } from '@core/api/inventory.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

const MOVEMENT_KINDS = [{ value: 'receipt', label: 'Receipt' }, { value: 'issue', label: 'Issue' }, { value: 'adjustment', label: 'Adjustment' }, { value: 'transferIn', label: 'Transfer in' }, { value: 'transferOut', label: 'Transfer out' }];

@Component({
  selector: 'erp-movements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzInputModule, DecimalPipe, RouterModule, NzButtonModule, NzIconModule, PagedTableComponent, StatusTagComponent, TranslatePipe],
  styles: [`
    .qty-badge {
      display: inline-flex;
      align-items: center;
      gap: 3px;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 13px;
    }
    .qty-in {
      background: var(--erp-color-success-bg);
      color: var(--erp-color-success-text);
    }
    .qty-out {
      background: var(--erp-color-error-bg);
      color: var(--erp-color-error-text);
    }
    .qty-neutral {
      background: var(--erp-color-bg-subtle);
      color: var(--erp-color-text-secondary);
    }
    .ref-pill {
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
      background: var(--erp-color-bg-subtle);
      border: 1px solid var(--erp-color-border-subtle);
      padding: 2px 7px;
      border-radius: 4px;
      color: var(--erp-color-text-primary);
    }
    .sub { color: var(--erp-color-text-secondary); margin: 4px 0 0; font-size: 13px; }
    .none { color: var(--erp-color-text-muted); }
  `],
  template: `
    <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
      <div>
        <h1 class="erp-page-title" style="margin:0;">{{ 'menu.inventory.movements' | translate: 'Stock movements' }}</h1>
        <p class="sub">Every receipt, issue, transfer and adjustment, newest first.</p>
      </div>
      <div style="display:flex; gap:8px;">
        <a routerLink="/inventory/stock" nz-button nzType="default">
          <span nz-icon nzType="appstore"></span> {{ 'menu.inventory.stock' | translate: 'Stock on hand' }}
        </a>
        <a routerLink="/inventory/warehouses" nz-button nzType="default">
          <span nz-icon nzType="home"></span> {{ 'menu.inventory.warehouses' | translate: 'Warehouses' }}
        </a>
      </div>
    </div>

    <erp-paged-table [list]="list" [columns]="columns()" searchPlaceholder="Search reference or memo" emptyText="No stock movements yet. They appear when stock is received, issued, moved or counted." [clickable]="false">
      <input aria-label="Exact reference, for example CNT-00001" toolbar nz-input style="width:170px" placeholder="Exact reference" [ngModel]="reference" (ngModelChange)="filterReference($event)" data-testid="movement-reference" />
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('kind') {
            <erp-status [value]="row.kind" />
          }
          @case ('quantity') {
            <span class="qty-badge" [ngClass]="row.quantity > 0 ? 'qty-in' : row.quantity < 0 ? 'qty-out' : 'qty-neutral'">
              <span nz-icon [nzType]="row.quantity > 0 ? 'arrow-down' : row.quantity < 0 ? 'arrow-up' : 'minus'" aria-hidden="true"></span>
              {{ row.quantity > 0 ? '+' : '' }}{{ row.quantity | number:'1.0-4' }}
            </span>
          }
          @case ('unitCost') {
            {{ currency() }} {{ row.unitCost | number:'1.2-2' }}
          }
          @case ('reference') {
            @if (row.reference) {
              <span class="ref-pill">{{ row.reference }}</span>
            } @else {
              <span class="none">—</span>
            }
          }
          @case ('journal') {
            @if (row.journalEntryId) {
              <a [routerLink]="['/finance/journals', row.journalEntryId]" nz-button nzType="link" nzSize="small">
                <span nz-icon nzType="book"></span> Journal
              </a>
            } @else {
              <span class="none">—</span>
            }
          }
          @default {
            {{ value || '—' }}
          }
        }
      </ng-template>
    </erp-paged-table>
  `,
})
export class MovementsPage {
  reference = '';
  filterReference(value: string): void {
    this.reference = value;
    this.list.setFilters(value.trim() ? [{ field: 'reference', op: 'eq', value: value.trim() }] : []);
  }

  private readonly api = inject(MovementsApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });
  /** The API sends only ids on a movement, so the names come from these two lists (200 rows each at most). */
  private readonly warehouses = toSignal(inject(WarehousesApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(map((r) => r.items), catchError(() => of([]))), { initialValue: [] });
  private readonly itemNames = toSignal(inject(ItemsApi).list({ page: 1, pageSize: 200 }).pipe(map((r) => new Map(r.items.map((i) => [i.id, `${i.sku} · ${i.name}`]))), catchError(() => of(new Map<string, string>()))), { initialValue: new Map<string, string>() });
  private readonly warehouseNames = computed(() => new Map(this.warehouses().map((w) => [w.id, w.name])));

  /** Only the date can be sorted on the server; kind and warehouse are server filters. */
  readonly columns = computed<TableColumn<MovementRow>[]>(() => [
    { key: 'date', label: 'Date', sortField: 'date', width: '120px' },
    { key: 'item', label: 'Item', value: (m) => this.itemNames().get(m.itemId) ?? '', filterField: 'itemId', filterOptions: [...this.itemNames()].map(([value, label]) => ({ value, label })) },
    { key: 'warehouse', label: 'Warehouse', value: (m) => this.warehouseNames().get(m.warehouseId) ?? '', filterField: 'warehouseId', filterOptions: this.warehouses().map((w) => ({ value: w.id, label: w.name })) },
    { key: 'kind', label: 'Type', width: '120px', filterField: 'kind', filterOptions: MOVEMENT_KINDS },
    { key: 'quantity', label: 'Quantity', width: '110px', align: 'right' },
    { key: 'unitCost', label: 'Unit cost', width: '120px', align: 'right' },
    { key: 'reference', label: 'Reference', width: '150px' },
    { key: 'memo', label: 'Notes', hidden: true },
    { key: 'journal', label: 'Ledger entry', width: '100px', filter: false },
  ]);
}
