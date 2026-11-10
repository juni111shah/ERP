import { Injectable, inject, signal } from '@angular/core';
import { ItemsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { WarehousesApi } from '@core/api/inventory.api';

/** A sellable item as the price list lines pick it. */
export interface RetailItemOption {
  id: string;
  name: string;
  sku: string;
  salesPrice?: number;
}

/**
 * The pick lists of the retail drawers: legal entities, active items and warehouses. Provided per page
 * (`providers: [RetailLookups]`), and each page loads only the lists it shows.
 */
@Injectable()
export class RetailLookups {
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly warehousesApi = inject(WarehousesApi);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly items = signal<RetailItemOption[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);

  loadEntities(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.entities.set(res.items),
    });
  }

  loadItems(): void {
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.items.set(res.items.filter((i) => i.isActive)),
    });
  }

  loadWarehouses(): void {
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.warehouses.set(res.items),
    });
  }

  item(id: string): RetailItemOption | undefined {
    return this.items().find((x) => x.id === id);
  }

  warehouseName(id: string | null | undefined): string {
    return this.warehouses().find((w) => w.id === id)?.name ?? '';
  }
}
