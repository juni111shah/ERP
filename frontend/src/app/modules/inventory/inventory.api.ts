import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AccountsApi, ItemsApi } from '../../core/api/admin.api';
import { API_BASE_URL } from '../../core/api/api-config';
import { CrudApiService } from '../../core/api/crud-api.service';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { SessionStore } from '../../core/session/session.store';
import { SelectOption } from '../../shared/record-drawer.component';

const BASE = 'm/inventory';

export interface WarehouseRow { id: string; code: string; name: string; legalEntityId: string; address?: string; isActive: boolean; }
export interface MovementRow { id: string; warehouseId: string; itemId: string; date: string; kind: string; quantity: number; unitCost: number; reference?: string; memo?: string; journalEntryId?: string; }
export interface StockRow { warehouseId: string; warehouse: string; itemId: string; sku: string; item: string; uom: string; quantity: number; averageCost: number; value: number; }
export interface StockOnHand { rows: StockRow[]; totalValue: number; }
export interface LowStockRow { itemId: string; sku: string; item: string; onHand: number; reorderLevel: number; reorderQuantity: number; }

@Injectable({ providedIn: 'root' })
export class WarehousesApi extends CrudApiService<WarehouseRow, unknown> { protected readonly resource = `${BASE}/warehouses`; }
@Injectable({ providedIn: 'root' })
export class MovementsApi extends CrudApiService<MovementRow, unknown> { protected readonly resource = `${BASE}/movements`; }

@Injectable({ providedIn: 'root' })
export class StockApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  onHand(): Observable<StockOnHand> { return this.http.get<StockOnHand>(`${this.base}/${BASE}/stock`); }
  low(): Observable<LowStockRow[]> { return this.http.get<LowStockRow[]>(`${this.base}/${BASE}/stock/low`); }
  act(action: 'receive' | 'issue' | 'adjust' | 'transfer', body: unknown): Observable<unknown> { return this.http.post(`${this.base}/${BASE}/stock/${action}`, body); }
  setPolicy(body: { itemId: string; reorderLevel: number; reorderQuantity: number }): Observable<unknown> { return this.http.put(`${this.base}/${BASE}/stock/policy`, body); }
}

@Injectable({ providedIn: 'root' })
export class InventoryLookups {
  private readonly warehouses = inject(WarehousesApi);
  private readonly items = inject(ItemsApi);
  private readonly accounts = inject(AccountsApi);
  private readonly session = inject(SessionStore);

  warehouse = (term: string): Observable<LookupOption[]> =>
    this.warehouses.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((w) => ({ value: w.id, label: `${w.code} · ${w.name}` }))));
  product = (term: string): Observable<LookupOption[]> =>
    this.items.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'type', op: 'eq', value: 'product' }] }).pipe(map((r) => r.items.map((i) => ({ value: i.id, label: `${i.sku} · ${i.name}` }))));
  account = (type?: string) => (term: string): Observable<LookupOption[]> =>
    this.accounts.list({ page: 1, pageSize: 20, search: term, filters: type ? [{ field: 'type', op: 'eq', value: type }] : [] }).pipe(map((r) => r.items.filter((a) => a.isPostable).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` }))));
  legalEntities(): SelectOption[] { return (this.session.me()?.legalEntities ?? []).map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` })); }
}
