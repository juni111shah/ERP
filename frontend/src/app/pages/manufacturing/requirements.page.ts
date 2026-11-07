import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ItemsApi } from '../../core/api/admin.api';
import { ManufacturingWorkOrdersApi, RequirementsDto } from '../../core/api/manufacturing.api';
import { problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { WarehouseRow, WarehousesApi } from '../../modules/inventory/inventory.api';
import { defaultLegalEntityId, legalEntityOptions } from '../../shared/entity-options';
import { StatePanelComponent } from '../../shared/state-panel.component';

/** Material requirements: what the open work orders (and an optional what-if quantity) need, against stock, and what is left to make or buy. */
@Component({
  selector: 'erp-requirements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzInputNumberModule, NzSelectModule, NzSkeletonModule, NzTableModule, NzTagModule, StatePanelComponent],
  styles: [`
    .bar { display: flex; gap: 12px; flex-wrap: wrap; align-items: flex-end; margin-bottom: 16px; }
    .field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: var(--erp-color-text-muted); }
    .r { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .short { font-weight: 700; color: var(--erp-color-error-text, #cf1322); }
    .note { margin: 0 0 12px; color: var(--erp-color-text-muted); }
  `],
  template: `
    <h1 class="erp-page-title">Material requirements</h1>
    <p class="note">What the open work orders need, level by level through the bills of materials, against what is on hand and in production. Add a quantity of one product to see what making it would need as well.</p>
    <div class="bar">
      <div class="field">Legal entity
        <nz-select [ngModel]="entityId()" (ngModelChange)="entityId.set($event)" style="width:240px" data-testid="req-entity">@for (o of entities; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label" /> }</nz-select>
      </div>
      <div class="field">Warehouse
        <nz-select [ngModel]="warehouseId()" (ngModelChange)="warehouseId.set($event)" nzAllowClear nzPlaceHolder="Those of the open orders" style="width:240px" data-testid="req-warehouse">@for (w of warehouses(); track w.id) { <nz-option [nzValue]="w.id" [nzLabel]="w.code + ' · ' + w.name" /> }</nz-select>
      </div>
      <div class="field">What-if product
        <nz-select [ngModel]="itemId()" (ngModelChange)="itemId.set($event)" nzAllowClear nzShowSearch nzPlaceHolder="None" style="width:240px" data-testid="req-item">@for (i of items(); track i.id) { <nz-option [nzValue]="i.id" [nzLabel]="i.sku + ' · ' + i.name" /> }</nz-select>
      </div>
      <div class="field">Quantity <nz-input-number [ngModel]="quantity()" (ngModelChange)="quantity.set($event)" [nzMin]="0" style="width:110px" data-testid="req-quantity" /></div>
      <button nz-button nzType="primary" type="button" [disabled]="!entityId()" (click)="run()" data-testid="req-run">Show requirements</button>
    </div>
    @if (loading()) {
      <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }" />
    } @else if (error()) {
      <erp-state-panel kind="error" title="Could not work out the requirements" [hint]="error()" actionLabel="Try again" (action)="run()" />
    } @else if (result(); as r) {
      <p data-testid="req-summary"><b>{{ r.openWorkOrders }}</b> open work orders. <nz-tag [nzColor]="r.shortages.length ? 'red' : 'green'">{{ r.shortages.length ? r.shortages.length + ' items short' : 'Nothing short' }}</nz-tag>
        @if (!r.openPurchaseNetted) { <span class="note">Open purchase orders are not counted, so a shortage may already be on order.</span> }</p>
      @if (r.rows.length) {
        <nz-table [nzData]="r.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="req-table">
          <thead><tr><th>Item</th><th>Level</th><th>Kind</th><th class="r">Required</th><th class="r">On hand</th><th class="r">On order</th><th class="r">In production</th><th class="r">To make or buy</th></tr></thead>
          <tbody>
            @for (row of r.rows; track row.itemId) {
              <tr>
                <td>{{ row.itemCode }} · {{ row.itemName }}</td><td>{{ row.level }}</td><td>{{ row.isManufactured ? 'Make' : 'Buy' }}</td>
                <td class="r">{{ row.grossRequired | number: '1.0-4' }} {{ row.uom }}</td><td class="r">{{ row.onHand | number: '1.0-4' }}</td><td class="r">{{ row.onOrder | number: '1.0-4' }}</td><td class="r">{{ row.inProduction | number: '1.0-4' }}</td>
                <td class="r" [class.short]="row.netRequired > 0">{{ row.netRequired | number: '1.0-4' }}</td>
              </tr>
            }
          </tbody>
        </nz-table>
      } @else { <erp-state-panel title="Nothing is required" hint="There are no open work orders and no what-if quantity." /> }
    }`,
})
export class RequirementsPage {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly session = inject(SessionStore);
  readonly entities = legalEntityOptions(this.session);
  readonly entityId = signal<string | null>(defaultLegalEntityId(this.session) ?? this.entities[0]?.value as string ?? null);
  readonly warehouseId = signal<string | null>(null);
  readonly itemId = signal<string | null>(null);
  readonly quantity = signal<number | null>(null);
  readonly warehouses = signal<WarehouseRow[]>([]);
  readonly items = signal<{ id: string; sku: string; name: string }[]>([]);
  readonly result = signal<RequirementsDto | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly shortCount = computed(() => this.result()?.shortages.length ?? 0);

  constructor() {
    inject(WarehousesApi).list({ page: 1, pageSize: 100, sort: 'code' }).subscribe({ next: (r) => this.warehouses.set(r.items.filter((w) => w.isActive)), error: () => undefined });
    inject(ItemsApi).list({ page: 1, pageSize: 200, sort: 'name' }).subscribe({ next: (r) => this.items.set(r.items.filter((i) => i.type === 'product').map((i) => ({ id: i.id, sku: i.sku, name: i.name }))), error: () => undefined });
    if (this.entityId()) this.run();
  }

  run(): void {
    const entity = this.entityId();
    if (!entity) return;
    // The server wants an item and a quantity together or neither.
    const whatIf = this.itemId() && (this.quantity() ?? 0) > 0;
    this.loading.set(true);
    this.error.set(null);
    this.api.requirements(entity, { warehouseId: this.warehouseId(), itemId: whatIf ? this.itemId() : null, quantity: whatIf ? this.quantity() : null }).subscribe({
      next: (r) => { this.result.set(r); this.loading.set(false); },
      error: (e) => { this.error.set(problemMessage(e, 'The requirements could not be worked out')); this.loading.set(false); },
    });
  }
}
