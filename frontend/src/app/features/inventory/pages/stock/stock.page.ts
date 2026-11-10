import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { toIsoDate } from '@core/format/dates';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SessionStore } from '@core/session/session.store';
import { InventoryLookups, LowStockRow, StockApi, StockOnHand } from '@core/api/inventory.api';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';

type Action = 'receive' | 'issue' | 'adjust' | 'transfer' | 'policy';

/** Stock on hand with its value, low-stock list and the actions that change it (receive, issue, adjust, transfer, reorder level). */
@Component({
  selector: 'erp-stock',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, StatePanelComponent, FormsModule, NzIconModule, NzSelectModule, TableToolsComponent, TranslatePipe, DecimalPipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzTableModule, NzTagModule],
  styleUrl: './stock.page.scss',
  templateUrl: './stock.page.html',
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
      next: () => { this.saving.set(false); this.current.set(null); this.message.success(action === 'policy' ? 'Reorder policy saved' : action === 'adjust' ? `Stock adjusted by ${values['quantityDelta']}` : action === 'receive' ? `${values['quantity']} received into stock` : action === 'issue' ? `${values['quantity']} issued from stock` : `${values['quantity']} transferred between warehouses`); this.load(); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save'); },
    });
  }
}
