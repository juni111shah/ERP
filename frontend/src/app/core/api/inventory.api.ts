import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AccountsApi, ItemsApi } from './admin.api';
import { API_BASE_URL } from './api-config';
import { PagedResult } from './api.models';
import { CrudApiService } from './crud-api.service';
import { LookupOption } from '../custom-fields/lookup-registry';
import { SessionStore } from '../session/session.store';
import { SelectOption } from '@shared/components/record-drawer/record-drawer.component';

const BASE = 'm/inventory';

export interface WarehouseRow { id: string; code: string; name: string; legalEntityId: string; address?: string; isActive: boolean; }
export interface CountRow { id: string; number: string; warehouseId: string; countDate: string; status: string; lines: number; memo?: string; }
export interface CountLine { itemId: string; sku: string; item: string; uom: string; systemQuantity: number; countedQuantity?: number | null; variance?: number | null; movementId?: string; }
export interface CountDetail { id: string; number: string; warehouseId: string; warehouse: string; countDate: string; status: string; memo?: string; postedAt?: string; lines: CountLine[]; }
export interface MovementRow { id: string; warehouseId: string; itemId: string; date: string; kind: string; quantity: number; unitCost: number; reference?: string; memo?: string; journalEntryId?: string; }
export interface StockRow { warehouseId: string; warehouse: string; itemId: string; sku: string; item: string; uom: string; quantity: number; averageCost: number; value: number; }
export interface StockOnHand { rows: StockRow[]; totalValue: number; }
export interface LowStockRow { itemId: string; sku: string; item: string; onHand: number; reorderLevel: number; reorderQuantity: number; }
/** What was found on the shelf for one item of a count; null when it has not been counted yet. */
export interface CountedQuantityInput { itemId: string; countedQuantity: number | null; }
/** The stock movements a person can book from the stock screen. */
export type StockAction = 'receive' | 'issue' | 'adjust' | 'transfer';
/** When an item counts as low and how much to reorder then. */
export interface ReorderPolicyInput { itemId: string; reorderLevel: number; reorderQuantity: number; }

@Injectable({ providedIn: 'root' })
export class WarehousesApi extends CrudApiService<WarehouseRow, unknown> {
  protected readonly resource = `${BASE}/warehouses`;
  /** The first `pageSize` warehouses in the server's own order (no page, sort or search sent). */
  firstPage(pageSize: number): Observable<PagedResult<WarehouseRow>> { return this.http.get<PagedResult<WarehouseRow>>(this.url, { params: { pageSize } }); }
}
@Injectable({ providedIn: 'root' })
export class CountsApi extends CrudApiService<CountRow, unknown> {
  protected readonly resource = `${BASE}/counts`;
  detail(id: string): Observable<CountDetail> { return this.http.get<CountDetail>(`${this.url}/${id}`); }
  saveLines(id: string, lines: CountedQuantityInput[]): Observable<CountDetail> { return this.http.put<CountDetail>(`${this.url}/${id}/lines`, { lines }); }
  post(id: string): Observable<CountDetail> { return this.http.post<CountDetail>(`${this.url}/${id}/post`, {}); }
  cancel(id: string): Observable<CountDetail> { return this.http.post<CountDetail>(`${this.url}/${id}/cancel`, {}); }
}
@Injectable({ providedIn: 'root' })
export class MovementsApi extends CrudApiService<MovementRow, unknown> { protected readonly resource = `${BASE}/movements`; }

@Injectable({ providedIn: 'root' })
export class StockApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  onHand(warehouseId?: string | null): Observable<StockOnHand> { return this.http.get<StockOnHand>(`${this.base}/${BASE}/stock`, { params: warehouseId ? { warehouseId } : {} }); }
  low(): Observable<LowStockRow[]> { return this.http.get<LowStockRow[]>(`${this.base}/${BASE}/stock/low`); }
  act(action: StockAction, body: unknown): Observable<unknown> { return this.http.post(`${this.base}/${BASE}/stock/${action}`, body); }
  setPolicy(body: ReorderPolicyInput): Observable<unknown> { return this.http.put(`${this.base}/${BASE}/stock/policy`, body); }
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
