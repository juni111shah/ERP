import { Injectable, inject, signal } from '@angular/core';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { WarehousesApi } from '@core/api/inventory.api';
import { PartyDto } from '@core/api/parties.api';

/** A record as the distribution pickers show it. */
export interface NamedOption {
  id: string;
  name: string;
}

/**
 * The legal-entity and warehouse pick lists of the distribution forms.
 * Provided per page (`providers: [DistributionLookups]`) so each page loads its own fresh lists.
 */
@Injectable()
export class DistributionLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly warehousesApi = inject(WarehousesApi);

  readonly entities = signal<NamedOption[]>([]);
  readonly warehouses = signal<NamedOption[]>([]);

  loadEntities(): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.entities.set(res.items),
    });
  }

  loadWarehouses(): void {
    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.warehouses.set(res.items),
    });
  }

  /** The entity chosen in the header when it is in the list, else the first one, else none (''). */
  defaultEntityId(currentEntityId: string | null): string {
    const entities = this.entities();
    return entities.find((e) => e.id === currentEntityId)?.id || entities[0]?.id || '';
  }
}

/** Parties offered as stop customers: those with no role or a customer role; with none of those, every party. */
export function customerOptions(parties: PartyDto[]): NamedOption[] {
  const customers = parties.filter((p) => !p.roles?.length || p.roles.some((r) => /customer/i.test(r)));
  return (customers.length > 0 ? customers : parties).map((p) => ({ id: p.id, name: p.name }));
}
