import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { InventoryLookups, LowStockRow, ReorderPolicyInput, StockApi, StockOnHand, StockRow } from '@core/api/inventory.api';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { STOCK_ACTIONS, stockActionBody, stockActionDone, stockActionFields, StockFormAction } from '../../inventory-stock';

/** The warehouses that appear in the stock rows, once each, for the warehouse filter. */
function warehousesOf(rows: StockRow[]): { id: string; name: string }[] {
  return [...new Map(rows.map((r) => [r.warehouseId, { id: r.warehouseId, name: r.warehouse }])).values()];
}

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
  private readonly stockApi = inject(StockApi);
  private readonly lookups = inject(InventoryLookups);
  private readonly message = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly actions = STOCK_ACTIONS;
  readonly data = signal<StockOnHand | null>(null);
  readonly low = signal<LowStockRow[]>([]);
  readonly failed = signal(false);
  private readonly session = inject(SessionStore);
  readonly canWrite = computed(() => this.session.hasPermission('inventory.stock.write'));
  /** Low stock is decided per item across all warehouses, so every warehouse row of the item is marked. */
  readonly lowIds = computed(() => new Set(this.low().map((l) => l.itemId)));
  readonly lowOnly = signal(false);
  readonly q = signal('');
  /** Server-side filter (GET /inventory/stock?warehouseId=); the search box filters the loaded rows by SKU, item or warehouse. */
  readonly warehouseId = signal<string | null>(null);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly shown = computed(() => (this.data()?.rows ?? []).filter((r) => matchText(this.q(), r.sku, r.item, r.warehouse) && (!this.lowOnly() || this.lowIds().has(r.itemId))));
  readonly current = signal<StockFormAction | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  private readonly schemas = stockActionFields(this.lookups, new Date());
  readonly fields = computed(() => this.schemas[this.current() ?? 'receive']);
  readonly title = computed(() => this.actions.find((a) => a.key === this.current())?.label ?? '');

  constructor() { this.load(); }

  load(): void {
    const wh = this.warehouseId();
    this.failed.set(false);
    this.stockApi.onHand(wh).subscribe({
      next: (d) => {
        this.data.set(d);
        if (!wh) this.warehouses.set(warehousesOf(d.rows));
      },
      error: () => this.failed.set(true),
    });
    // A reorder level of 0 means no reorder rule, so an item at 0 with no level is not low.
    this.stockApi.low().subscribe({
      next: (all) => {
        const l = all.filter((x) => x.reorderLevel > 0);
        this.low.set(l);
        if (!l.length) this.lowOnly.set(false);
      },
      error: () => this.low.set([]),
    });
  }

  filterWarehouse(id: string | null): void { this.warehouseId.set(id); this.load(); }
  open(a: StockFormAction): void { if (!this.canWrite()) return; this.errors.set(null); this.current.set(a); }

  submit(values: Record<string, unknown>): void {
    const action = this.current();
    if (!action) return;
    const body = stockActionBody(values);
    this.saving.set(true);
    const call = action === 'policy' ? this.stockApi.setPolicy(body as unknown as ReorderPolicyInput) : this.stockApi.act(action, body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.current.set(null);
        this.message.success(stockActionDone(action, values));
        this.load();
      },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        this.errors.set(p);
        this.message.error(p?.detail ?? p?.title ?? 'Could not save');
      },
    });
  }
}
