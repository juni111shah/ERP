import { Injectable, inject, signal } from '@angular/core';
import { LegalEntitiesApi } from '@core/api/admin.api';

/** A legal entity as the manufacturing pickers show it. */
export interface EntityOption {
  id: string;
  name: string;
}

/**
 * The legal-entity pick list of the bill-of-materials and work-order forms.
 * Provided per page (`providers: [ManufacturingLookups]`) so each page loads its own fresh list.
 */
@Injectable()
export class ManufacturingLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);

  readonly legalEntities = signal<EntityOption[]>([]);

  loadEntities(): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.legalEntities.set(res.items.map((e) => ({ id: e.id, name: e.name }))),
    });
  }

  /** The entity chosen in the header when it is in the list, else the first one. */
  defaultEntity(currentEntityId: string | null): EntityOption | undefined {
    return this.legalEntities().find((e) => e.id === currentEntityId) ?? this.legalEntities()[0];
  }
}
