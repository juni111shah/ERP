import { Injectable, inject, signal } from '@angular/core';
import { ItemDto, ItemsApi, LegalEntitiesApi, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { loadInto } from '@shared/utils/load-into';

/**
 * The pick lists of the quote and sales-order forms: legal entities, customers, items and tax codes (with the default rate).
 * Provided per page (`providers: [SalesLookups]`) so each page loads its own fresh lists.
 */
@Injectable()
export class SalesLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);

  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly customers = signal<PartyDto[]>([]);
  readonly items = signal<ItemDto[]>([]);
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  /** The default tax code's rate: the one marked default, else the first VAT / sales tax, else the first. */
  readonly defaultTaxRate = signal<number>(0);

  load(): void {
    loadInto(this.entities, this.entitiesApi.list({ page: 1, pageSize: 50 }), (res) => res.items.map((x) => ({ id: x.id, name: x.name })));
    // Parties with no role yet count as customers.
    loadInto(this.customers, this.partiesApi.list({ page: 1, pageSize: 200 }), (res) =>
      res.items.filter((p) => !p.roles || p.roles.length === 0 || p.roles.some((r) => r.toLowerCase() === 'customer')));
    loadInto(this.items, this.itemsApi.list({ page: 1, pageSize: 200 }), (res) => res.items);
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

  customer(id: string | null | undefined): PartyDto | undefined {
    return this.customers().find((x) => x.id === id);
  }

  item(id: string): ItemDto | undefined {
    return this.items().find((x) => x.id === id);
  }
}
