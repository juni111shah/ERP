import { CreateRouteInput, DeliveryStopDto, DeliveryStopInput, DistributionRouteDto } from '@core/api/distribution.api';

/** The route form's raw value (`getRawValue()`, so the locked legal entity is included). */
export interface RouteFormValue {
  legalEntityId: string;
  code: string;
  name: string;
  description: string;
  assignedDriverUserId: string | null;
  driverName: string;
  vehicleNumber: string;
  vehicleCapacityKg: number | null;
  warehouseId: string | null;
  isActive: boolean;
}

/** The add-stop form's raw value. */
export interface StopFormValue {
  customerPartyId: string;
  customerName: string;
  sequenceOrder: number;
  address: string;
  contactPhone: string;
  preferredTimeWindow: string;
}

/** The route form's values when a new route is created. */
export function emptyRouteForm(legalEntityId: string): RouteFormValue {
  return {
    legalEntityId,
    code: '',
    name: '',
    description: '',
    assignedDriverUserId: null,
    driverName: '',
    vehicleNumber: '',
    vehicleCapacityKg: null,
    warehouseId: null,
    isActive: true,
  };
}

/** The route form's values for an existing route. */
export function routeFormOf(route: DistributionRouteDto): RouteFormValue {
  return {
    legalEntityId: route.legalEntityId,
    code: route.code,
    name: route.name,
    description: route.description ?? '',
    assignedDriverUserId: route.assignedDriverUserId ?? null,
    driverName: route.driverName ?? '',
    vehicleNumber: route.vehicleNumber ?? '',
    vehicleCapacityKg: route.vehicleCapacityKg ?? null,
    warehouseId: route.warehouseId ?? null,
    isActive: route.isActive,
  };
}

/** The route as the API takes it. Editing sends no stops, which leaves the stops as they are. */
export function toRouteInput(val: RouteFormValue, editing: DistributionRouteDto | null): CreateRouteInput {
  return {
    legalEntityId: val.legalEntityId,
    code: val.code,
    name: val.name,
    description: val.description,
    assignedDriverUserId: val.assignedDriverUserId,
    driverName: val.driverName,
    vehicleNumber: val.vehicleNumber,
    vehicleCapacityKg: val.vehicleCapacityKg,
    warehouseId: val.warehouseId,
    isActive: val.isActive,
    stops: editing ? undefined : [],
    customFields: editing?.customFields ?? null,
  };
}

/** Existing stops as the API takes them back. */
export function toStopInputs(stops: DeliveryStopDto[]): DeliveryStopInput[] {
  return stops.map((s) => ({
    customerPartyId: s.customerPartyId,
    customerName: s.customerName,
    sequenceOrder: s.sequenceOrder,
    address: s.address,
    contactPhone: s.contactPhone,
    preferredTimeWindow: s.preferredTimeWindow,
    specialInstructions: s.specialInstructions,
  }));
}

/** A new stop from the add-stop form. */
export function toNewStopInput(stop: StopFormValue): DeliveryStopInput {
  return {
    customerPartyId: stop.customerPartyId,
    customerName: stop.customerName,
    sequenceOrder: stop.sequenceOrder,
    address: stop.address,
    contactPhone: stop.contactPhone,
    preferredTimeWindow: stop.preferredTimeWindow,
    specialInstructions: '',
  };
}

/** An update replaces the whole route, so every field goes back with the changed stops. */
export function routeWithStops(route: DistributionRouteDto, stops: DeliveryStopInput[]): Partial<CreateRouteInput> {
  return {
    name: route.name,
    description: route.description,
    assignedDriverUserId: route.assignedDriverUserId,
    driverName: route.driverName,
    vehicleNumber: route.vehicleNumber,
    vehicleCapacityKg: route.vehicleCapacityKg,
    warehouseId: route.warehouseId,
    isActive: route.isActive,
    stops,
    customFields: route.customFields,
  };
}

/** Whether the route read back after a save holds what was sent (name, driver, van, status and, when sent, the stops). */
export function routeKeptChange(saved: DistributionRouteDto, input: Partial<CreateRouteInput>): boolean {
  const stopKey = (stops: { customerPartyId: string; sequenceOrder: number }[]) =>
    stops.map((s) => `${s.sequenceOrder}:${s.customerPartyId}`).sort().join();
  return saved.name === input.name?.trim()
    && (saved.driverName ?? '') === (input.driverName ?? '').trim()
    && (saved.vehicleNumber ?? '') === (input.vehicleNumber ?? '').trim()
    && saved.isActive === input.isActive
    && (!input.stops || stopKey(saved.stops) === stopKey(input.stops));
}
