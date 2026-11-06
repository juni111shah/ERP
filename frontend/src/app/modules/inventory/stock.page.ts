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
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SessionStore } from '../../core/session/session.store';
import { InventoryLookups, LowStockRow, StockApi, StockOnHand } from './inventory.api';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';

type Action = 'receive' | 'issue' | 'adjust' | 'transfer' | 'policy';

/** Stock on hand with its value, low-stock list and the actions that change it (receive, issue, adjust, transfer, reorder level). */
@Component({
  selector: 'erp-stock',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, StatePanelComponent, FormsModule, NzIconModule, NzSelectModule, TableToolsComponent, TranslatePipe, DecimalPipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzTableModule, NzTagModule],
  styles: [`
    .low-panel { margin-bottom: 16px; border: 1px solid var(--erp-color-warning-border); border-radius: 12px; background: var(--erp-color-bg-surface); overflow: hidden; }
    .low-head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding: 10px 16px; background: var(--erp-color-warning-bg); color: var(--erp-color-warning-text); font-weight: 600; }
    .low-head .grow { flex: 1; }
    .num { text-align: end; white-space: nowrap; }
    tr.low-row > td { background: var(--erp-color-warning-bg); }
    .low-tag { margin-inline-end: 8px; }
  `],
  template: `
    <h1 class="erp-page-title">Stock on hand</h1>
    <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px" *erpHasPermission="'inventory.stock.write'">
      @for (a of actions; track a.key) { <button nz-button [nzType]="a.key === 'receive' ? 'primary' : 'default'" (click)="open(a.key)" [attr.data-testid]="'stock-' + a.key">{{ a.label }}</button> }
    </div>
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="stock-kpis">
      <erp-stat-card label="Stock value" [loading]="!data()" [value]="data()?.totalValue" format="money" [tone]="0" />
      <erp-stat-card label="Item lines" [loading]="!data()" [value]="data()?.rows?.length" format="number" caption="item / warehouse" [tone]="2" />
      <erp-stat-card label="Low stock" [loading]="!data()" [value]="low().length" format="number" [caption]="low().length ? 'at or below the reorder level' : 'nothing at or below its reorder level'" [tone]="low().length > 0 ? 'danger' : 'neutral'" testId="stock-low-kpi" />
    </div>
    @if (low().length) {
      <section class="low-panel" aria-label="Low stock" data-testid="low-stock">
        <div class="low-head">
          <span nz-icon nzType="warning" aria-hidden="true"></span>
          <span class="grow">{{ low().length === 1 ? '1 item needs' : low().length + ' items need' }} reordering</span>
          <button nz-button nzSize="small" [attr.aria-pressed]="lowOnly()" (click)="lowOnly.set(!lowOnly())" data-testid="stock-low-only">{{ lowOnly() ? 'Show all stock' : 'Show only these below' }}</button>
        </div>
        <nz-table [nzData]="low()" nzSize="small" [nzFrontPagination]="false" [nzShowPagination]="false" data-testid="low-stock-table">
          <thead><tr><th>SKU</th><th>Item</th><th class="num">On hand</th><th class="num">Reorder at</th><th class="num">Order quantity</th></tr></thead>
          <tbody>
            @for (l of low(); track l.itemId) {
              <tr><td><span class="code-pill">{{ l.sku }}</span></td><td>{{ l.item }}</td><td class="num"><b>{{ l.onHand | number: '1.0-4' }}</b></td><td class="num">{{ l.reorderLevel | number: '1.0-4' }}</td><td class="num">{{ l.reorderQuantity | number: '1.0-4' }}</td></tr>
            }
          </tbody>
        </nz-table>
      </section>
    }
    <erp-table-tools [(search)]="q" (refresh)="load()" testId="stock">
      <nz-select [ngModel]="warehouseId()" (ngModelChange)="filterWarehouse($event)" nzAllowClear nzShowSearch [nzPlaceHolder]="'ui.common.filterWarehouse' | translate: 'Filter by warehouse'" style="width:200px" data-testid="stock-warehouse-filter">
        @for (w of warehouses(); track w.id) { <nz-option [nzValue]="w.id" [nzLabel]="w.name"></nz-option> }
      </nz-select>
    </erp-table-tools>
    @if (failed()) {
      <erp-state-panel kind="error" title="Stock could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
    <div style="background:var(--erp-color-bg-surface, #ffffff);border:1px solid var(--erp-color-border-subtle, #edf2f7);border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.02)">
      <nz-table [nzData]="shown()" [nzLoading]="!data()" nzSize="middle" [nzFrontPagination]="false" [nzShowPagination]="false" [nzNoResult]="noStock" data-testid="stock-table">
        <thead><tr><th>SKU</th><th>Item</th><th>Warehouse</th><th style="text-align:end">Quantity</th><th style="text-align:end">Avg cost</th><th style="text-align:end">Value</th></tr></thead>
        <tbody>
          @for (r of shown(); track r.warehouseId + r.itemId) {
            <tr [class.low-row]="lowIds().has(r.itemId)">
              <td><span class="code-pill">{{ r.sku }}</span></td>
              <td><span class="name-cell">{{ r.item }}</span></td>
              <td><nz-tag>{{ r.warehouse }}</nz-tag></td>
              <td style="text-align:end;white-space:nowrap">@if (lowIds().has(r.itemId)) { <nz-tag class="low-tag" nzColor="orange">Low</nz-tag> }{{ r.quantity | number: '1.0-4' }} {{ r.uom }}</td>
              <td style="text-align:end;white-space:nowrap">{{ currency() }} {{ r.averageCost | number: '1.2-2' }}</td>
              <td style="text-align:end;white-space:nowrap"><b>{{ currency() }} {{ r.value | number: '1.2-2' }}</b></td>
            </tr>
          }
        </tbody>
        @if (data(); as d) { <tfoot><tr><td colspan="5"><b>Total value</b></td><td style="text-align:end;white-space:nowrap"><b data-testid="stock-total">{{ currency() }} {{ d.totalValue | number: '1.2-2' }}</b></td></tr></tfoot> }
      </nz-table>
    </div>
    <ng-template #noStock>
      @if (lowOnly()) { <span>These items have no stock in any warehouse.</span> }
      @else if (data()?.rows?.length) { <span>No stock matches this search.</span> }
      @else { <erp-state-panel title="No stock yet" hint="Receive stock to get started." icon="inbox" [actionLabel]="canWrite ? 'Receive stock' : null" (action)="open('receive')" /> }
    </ng-template>
    }
    <erp-record-drawer [title]="title()" [open]="current() !== null" [fields]="fields()" [value]="null" [saving]="saving()" [errors]="errors()" testId="stock-form" [width]="480"
                       (submitted)="submit($event)" (closed)="current.set(null)" />`,
})
export class StockPage {
  private readonly api = inject(StockApi);
  private readonly lookups = inject(InventoryLookups);
  private readonly message = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly actions: { key: Action; label: string }[] = [{ key: 'receive', label: 'Receive stock' }, { key: 'issue', label: 'Issue stock' }, { key: 'adjust', label: 'Adjust' }, { key: 'transfer', label: 'Transfer' }, { key: 'policy', label: 'Reorder level' }];
  readonly data = signal<StockOnHand | null>(null);
  readonly low = signal<LowStockRow[]>([]);
  readonly failed = signal(false);
  readonly canWrite = inject(SessionStore).hasPermission('inventory.stock.write');
  /** Low stock is decided per item across all warehouses, so every warehouse row of the item is marked. */
  readonly lowIds = computed(() => new Set(this.low().map((l) => l.itemId)));
  readonly lowOnly = signal(false);
  readonly q = signal('');
  /** Server-side filter (GET /inventory/stock?warehouseId=); the search box filters the loaded rows by SKU, item or warehouse. */
  readonly warehouseId = signal<string | null>(null);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly shown = computed(() => (this.data()?.rows ?? []).filter((r) => matchText(this.q(), r.sku, r.item, r.warehouse) && (!this.lowOnly() || this.lowIds().has(r.itemId))));
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
    this.failed.set(false);
    this.api.onHand(wh).subscribe({
      next: (d) => {
        this.data.set(d);
        if (!wh) this.warehouses.set([...new Map(d.rows.map((r) => [r.warehouseId, { id: r.warehouseId, name: r.warehouse }])).values()]);
      },
      error: () => this.failed.set(true),
    });
    // A reorder level of 0 means no reorder rule, so an item at 0 with no level is not low.
    this.api.low().subscribe({ next: (all) => { const l = all.filter((x) => x.reorderLevel > 0); this.low.set(l); if (!l.length) this.lowOnly.set(false); }, error: () => this.low.set([]) });
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
