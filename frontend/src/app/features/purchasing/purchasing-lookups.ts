import { Injectable, inject, signal } from '@angular/core';
import { ItemDto, ItemsApi, LegalEntitiesApi, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { loadInto } from '@shared/utils/load-into';

/** A legal entity as the purchasing pickers show it. */
export interface EntityOption {
  id: string;
  name: string;
}

/**
 * The pick lists of the purchasing forms: legal entities, vendors, items and tax codes (with the default rate).
 * Provided per page (`providers: [PurchasingLookups]`) so each page loads its own fresh lists.
 */
@Injectable()
export class PurchasingLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);

  readonly entities = signal<EntityOption[]>([]);
  readonly vendors = signal<PartyDto[]>([]);
  readonly items = signal<ItemDto[]>([]);
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  /** The default tax code's rate: the one marked default, else the first VAT / sales tax, else the first. */
  readonly defaultTaxRate = signal<number>(0);

  /** The purchase-order form's lists. A role naming a vendor or supplier counts; with none, every party is offered. */
  loadForOrders(): void {
    this.loadEntities();
    loadInto(this.vendors, this.partiesApi.list({ page: 1, pageSize: 200 }), (res) => {
      const vendorParties = res.items.filter((p) => p.roles?.some((r) => r.toLowerCase().includes('vendor') || r.toLowerCase().includes('supplier')));
      return vendorParties.length > 0 ? vendorParties : res.items;
    });
    loadInto(this.items, this.itemsApi.list({ page: 1, pageSize: 200 }), (res) => res.items);
    this.loadTaxCodes();
  }

  /** The requisition page's lists: only parties with the vendor or supplier role, only active items. */
  loadForRequisitions(): void {
    this.loadEntities();
    loadInto(this.vendors, this.partiesApi.list({ page: 1, pageSize: 200 }), (res) =>
      res.items.filter((p) => p.roles?.some((r) => r.toLowerCase() === 'vendor' || r.toLowerCase() === 'supplier')));
    loadInto(this.items, this.itemsApi.list({ page: 1, pageSize: 200, filters: [{ field: 'isActive', op: 'eq', value: true }] }), (res) => res.items);
  }

  vendor(id: string | null | undefined): PartyDto | undefined {
    return this.vendors().find((x) => x.id === id);
  }

  item(id: string | null | undefined): ItemDto | undefined {
    return this.items().find((x) => x.id === id);
  }

  private loadEntities(): void {
    loadInto(this.entities, this.entitiesApi.list({ page: 1, pageSize: 50 }), (res) => res.items.map((x) => ({ id: x.id, name: x.name })));
  }

  private loadTaxCodes(): void {
    this.taxCodesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => {
        const codes = res.items || [];
        this.taxCodes.set(codes);
        const preferred = codes.find((t) => t.isDefault) || codes.find((t) => t.kind === 'vat' || t.kind === 'salesTax') || codes[0];
        if (preferred && typeof preferred.rate === 'number') this.defaultTaxRate.set(preferred.rate);
      },
      error: () => {},
    });
  }
}
