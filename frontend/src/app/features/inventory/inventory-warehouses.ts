import { Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';

/**
 * The warehouses (the first 200 by code) as a signal, for naming the warehouse ids the inventory lists carry.
 * Empty until loaded, and when they cannot be read. Call it in an injection context (a field initializer).
 */
export function injectWarehouseList(): Signal<WarehouseRow[]> {
  return toSignal(inject(WarehousesApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(
    map((r) => r.items),
    catchError(() => of([] as WarehouseRow[])),
  ), { initialValue: [] as WarehouseRow[] });
}

/** Warehouse names by id. */
export function warehouseNameMap(warehouses: readonly WarehouseRow[]): Map<string, string> {
  return new Map(warehouses.map((w) => [w.id, w.name]));
}
