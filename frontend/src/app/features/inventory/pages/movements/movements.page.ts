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
  styleUrl: './movements.page.scss',
  templateUrl: './movements.page.html',
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
