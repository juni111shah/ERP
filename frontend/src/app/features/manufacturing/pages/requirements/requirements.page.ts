import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ItemsApi } from '@core/api/admin.api';
import { ManufacturingWorkOrdersApi, RequirementsDto } from '@core/api/manufacturing.api';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** Material requirements: what the open work orders (and an optional what-if quantity) need, against stock, and what is left to make or buy. */
@Component({
  selector: 'erp-requirements',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzInputNumberModule, NzSelectModule, NzSkeletonModule, NzTableModule, NzTagModule, StatePanelComponent],
  styleUrl: './requirements.page.scss',
  templateUrl: './requirements.page.html',
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
