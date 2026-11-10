import { ItemDto } from '@core/api/admin.api';
import { CompleteTripInput, RecordTripItemReconciliationInput, TripInventoryInput, VanSalesTripDto } from '@core/api/distribution.api';

/** A catalog product that can be loaded onto a van. */
export interface TripCatalogItem {
  id: string;
  name: string;
  sku: string;
  uom: string;
  salesPrice?: number;
}

/** One item loaded onto the van in the dispatch dialog. */
export type LoadedItem = TripInventoryInput;

/** One row of the end-of-day reconciliation: what was loaded and what became of it. */
export interface ReconcileLine extends RecordTripItemReconciliationInput {
  itemName: string;
  loadedQuantity: number;
  unitPrice: number;
}

/** The dispatch form's raw value. */
export interface TripFormValue {
  legalEntityId: string;
  routeId: string;
  driverUserId: string | null;
  driverName: string;
  vehicleNumber: string;
  warehouseId: string;
  departureDate: Date;
}

/** Products that can go on a van: active product items. */
export function vanProducts(items: ItemDto[]): TripCatalogItem[] {
  return items.filter((i) => i.type === 'product' && i.isActive);
}

/** The load with `quantity` more of `item`: added to its row when it is already loaded, else a new row. */
export function addToLoad(current: LoadedItem[], item: TripCatalogItem, quantity: number): LoadedItem[] {
  const existingIdx = current.findIndex((i) => i.itemId === item.id);
  if (existingIdx >= 0) {
    current[existingIdx].loadedQuantity += quantity;
    return [...current];
  }
  return [
    ...current,
    {
      itemId: item.id,
      itemCode: item.sku,
      itemName: item.name,
      uom: item.uom || 'PCS',
      loadedQuantity: quantity,
      unitPrice: item.salesPrice ?? 0,
    },
  ];
}

/** The reconciliation rows of a trip: by default everything loaded counts as sold. */
export function toReconcileLines(trip: VanSalesTripDto): ReconcileLine[] {
  return trip.inventoryItems.map((i) => ({
    itemId: i.itemId,
    itemName: i.itemName,
    loadedQuantity: i.loadedQuantity,
    soldQuantity: i.soldQuantity || i.loadedQuantity,
    returnedQuantity: i.returnedQuantity || 0,
    damagedQuantity: i.damagedQuantity || 0,
    unitPrice: i.unitPrice,
  }));
}

/** What was sold, rounded to cents: the server refuses amounts with more decimals. */
export function tripSalesTotal(lines: ReconcileLine[]): number {
  return Math.round(lines.reduce((acc, curr) => acc + (curr.soldQuantity * curr.unitPrice), 0) * 100) / 100;
}

/** The first row where sold, returned and damaged add up to more than was loaded. */
export function overAccountedLine(lines: ReconcileLine[]): ReconcileLine | undefined {
  return lines.find((l) => l.soldQuantity + l.returnedQuantity + l.damagedQuantity > l.loadedQuantity);
}

/** The loaded items as the API takes them. */
export function toTripItems(loaded: LoadedItem[]): TripInventoryInput[] {
  return loaded.map((i) => ({
    itemId: i.itemId,
    itemCode: i.itemCode,
    itemName: i.itemName,
    uom: i.uom,
    loadedQuantity: i.loadedQuantity,
    unitPrice: i.unitPrice,
  }));
}

/** The complete-trip body. */
export function toCompleteTripInput(
  tripId: string,
  lines: ReconcileLine[],
  totals: { sales: number; collections: number },
  customerPartyId: string | null,
  allowUnaccounted: boolean,
): CompleteTripInput {
  return {
    tripId,
    totalSalesAmount: totals.sales,
    totalCollectionsAmount: totals.collections,
    items: lines.map((l) => ({
      itemId: l.itemId,
      soldQuantity: l.soldQuantity,
      returnedQuantity: l.returnedQuantity,
      damagedQuantity: l.damagedQuantity,
    })),
    customerPartyId,
    allowUnaccounted,
    notes: 'End of day field van reconciliation',
  };
}
