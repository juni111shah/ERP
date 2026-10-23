import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '../../core/api/api.models';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { InventoryLookups, LowStockRow, StockApi, StockOnHand } from './inventory.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';

type Action = 'receive' | 'issue' | 'adjust' | 'transfer' | 'policy';

/** Stock on hand with its value, low-stock list and the actions that change it (receive, issue, adjust, transfer, reorder level). */
@Component({
  selector: 'erp-stock',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSelectModule, TableToolsComponent, TranslatePipe, DecimalPipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzTableModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Stock on hand</h1>
    <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px" *erpHasPermission="'inventory.stock.write'">
      @for (a of actions; track a.key) { <button nz-button [nzType]="a.key === 'receive' ? 'primary' : 'default'" (click)="open(a.key)" [attr.data-testid]="'stock-' + a.key">{{ a.label }}</button> }
    </div>
    @if (low().length) {
      <div style="margin-bottom:12px" data-testid="low-stock">@for (l of low(); track l.itemId) { <nz-tag nzColor="orange">{{ l.sku }}: {{ l.onHand }} {{ 'ui.inv.left' | translate: 'left' }} ({{ 'ui.inv.reorderAt' | translate: 'reorder at' }} {{ l.reorderLevel }})</nz-tag> }</div>
    }
    <erp-table-tools [(search)]="q" (refresh)="load()" testId="stock">
      <nz-select [ngModel]="warehouseId()" (ngModelChange)="filterWarehouse($event)" nzAllowClear nzShowSearch [nzPlaceHolder]="'ui.common.filterWarehouse' | translate: 'Filter by warehouse'" style="width:200px" data-testid="stock-warehouse-filter">
        @for (w of warehouses(); track w.id) { <nz-option [nzValue]="w.id" [nzLabel]="w.name"></nz-option> }
      </nz-select>
    </erp-table-tools>
    <nz-table [nzData]="shown()" nzSize="small" [nzShowPagination]="false" data-testid="stock-table">
      <thead><tr><th>SKU</th><th>Item</th><th>Warehouse</th><th style="text-align:right">Quantity</th><th style="text-align:right">Avg cost</th><th style="text-align:right">Value</th></tr></thead>
      <tbody>
        @for (r of shown(); track r.warehouseId + r.itemId) {
          <tr><td>{{ r.sku }}</td><td>{{ r.item }}</td><td>{{ r.warehouse }}</td><td style="text-align:right">{{ r.quantity | number: '1.0-4' }} {{ r.uom }}</td><td style="text-align:right">{{ r.averageCost | number: '1.2-2' }}</td><td style="text-align:right">{{ r.value | number: '1.2-2' }}</td></tr>
        } @empty { <tr><td colspan="6">No stock yet. Receive stock to get started.</td></tr> }
      </tbody>
      @if (data(); as d) { <tfoot><tr><td colspan="5"><b>Total value</b></td><td style="text-align:right"><b data-testid="stock-total">{{ d.totalValue | number: '1.2-2' }}</b></td></tr></tfoot> }
    </nz-table>
    <erp-record-drawer [title]="title()" [open]="current() !== null" [fields]="fields()" [value]="null" [saving]="saving()" [errors]="errors()" testId="stock-form" [width]="480"
                       (submitted)="submit($event)" (closed)="current.set(null)" />`,
})
export class StockPage {
  private readonly api = inject(StockApi);
  private readonly lookups = inject(InventoryLookups);
  private readonly message = inject(NzMessageService);
  readonly actions: { key: Action; label: string }[] = [{ key: 'receive', label: 'Receive stock' }, { key: 'issue', label: 'Issue stock' }, { key: 'adjust', label: 'Adjust' }, { key: 'transfer', label: 'Transfer' }, { key: 'policy', label: 'Reorder level' }];
  readonly data = signal<StockOnHand | null>(null);
  readonly low = signal<LowStockRow[]>([]);
  readonly q = signal('');
  /** Server-side filter (GET /inventory/stock?warehouseId=); the search box filters the loaded rows by SKU, item or warehouse. */
  readonly warehouseId = signal<string | null>(null);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly shown = computed(() => (this.data()?.rows ?? []).filter((r) => matchText(this.q(), r.sku, r.item, r.warehouse)));
  readonly current = signal<Action | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  private readonly today = new Date();
  private readonly common = {
    warehouse: { key: 'warehouseId', label: 'Warehouse', type: 'lookup', lookup: this.lookups.warehouse, required: true } as FieldSpec,
    item: { key: 'itemId', label: 'Item', type: 'lookup', lookup: this.lookups.product, required: true } as FieldSpec,
    date: { key: 'date', label: 'Date', type: 'date', required: true, default: this.today } as FieldSpec,
    reference: { key: 'reference', label: 'Reference', type: 'text', maxLength: 100 } as FieldSpec,
    memo: { key: 'memo', label: 'Memo', type: 'text', maxLength: 500 } as FieldSpec,
  };
  private readonly schemas: Record<Action, FieldSpec[]> = {
    receive: [this.common.warehouse, this.common.item, { key: 'quantity', label: 'Quantity', type: 'number', min: 0.0001, precision: 4, required: true }, { key: 'unitCost', label: 'Unit cost', type: 'number', min: 0, default: 0, required: true },
      this.common.date, { key: 'offsetAccountId', label: 'Credit account (payable / bank)', type: 'lookup', lookup: this.lookups.account(), required: true, hint: 'Where the other side of the entry goes.' }, this.common.reference, this.common.memo],
    issue: [this.common.warehouse, this.common.item, { key: 'quantity', label: 'Quantity', type: 'number', min: 0.0001, precision: 4, required: true }, this.common.date,
      { key: 'expenseAccountId', label: 'Expense account', type: 'lookup', lookup: this.lookups.account('expense'), hint: 'Defaults to Cost of goods sold.' }, this.common.reference, this.common.memo],
    adjust: [this.common.warehouse, this.common.item, { key: 'quantityDelta', label: 'Change in quantity (+/-)', type: 'number', precision: 4, required: true }, { key: 'unitCost', label: 'Unit cost (for increases)', type: 'number', min: 0, default: 0 }, this.common.date, this.common.memo],
    transfer: [{ ...this.common.warehouse, key: 'fromWarehouseId', label: 'From warehouse' }, { ...this.common.warehouse, key: 'toWarehouseId', label: 'To warehouse' }, this.common.item, { key: 'quantity', label: 'Quantity', type: 'number', min: 0.0001, precision: 4, required: true }, this.common.date, this.common.memo],
    policy: [this.common.item, { key: 'reorderLevel', label: 'Reorder level', type: 'number', min: 0, precision: 4, required: true, default: 0 }, { key: 'reorderQuantity', label: 'Reorder quantity', type: 'number', min: 0, precision: 4, default: 0 }],
  };
  readonly fields = computed(() => this.schemas[this.current() ?? 'receive']);
  readonly title = computed(() => this.actions.find((a) => a.key === this.current())?.label ?? '');

  constructor() { this.load(); }

  load(): void {
    const wh = this.warehouseId();
    this.api.onHand(wh).subscribe((d) => {
      this.data.set(d);
      if (!wh) this.warehouses.set([...new Map(d.rows.map((r) => [r.warehouseId, { id: r.warehouseId, name: r.warehouse }])).values()]);
    });
    this.api.low().subscribe((l) => this.low.set(l));
  }

  filterWarehouse(id: string | null): void { this.warehouseId.set(id); this.load(); }
  open(a: Action): void { this.errors.set(null); this.current.set(a); }

  submit(values: Record<string, unknown>): void {
    const action = this.current();
    if (!action) return;
    const body = { ...values, ...(values['date'] ? { date: typeof values['date'] === 'string' ? values['date'] : toIsoDate(values['date'] as Date) } : {}) };
    this.saving.set(true);
    const call = action === 'policy' ? this.api.setPolicy(body as never) : this.api.act(action, body);
    call.subscribe({
      next: () => { this.saving.set(false); this.current.set(null); this.message.success('Saved'); this.load(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }
}
