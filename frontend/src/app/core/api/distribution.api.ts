import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type TripStatus = 'Scheduled' | 'InProgress' | 'Completed' | 'Cancelled';
export type WholesaleTier = 'Standard' | 'Bronze' | 'Silver' | 'Gold' | 'Platinum';

export interface DeliveryStopDto {
  id: string;
  routeId: string;
  customerPartyId: string;
  customerName: string;
  sequenceOrder: number;
  address?: string | null;
  contactPhone?: string | null;
  preferredTimeWindow?: string | null;
  specialInstructions?: string | null;
  isActive: boolean;
}

export interface DeliveryStopInput {
  customerPartyId: string;
  customerName: string;
  sequenceOrder: number;
  address?: string | null;
  contactPhone?: string | null;
  preferredTimeWindow?: string | null;
  specialInstructions?: string | null;
}

export interface DistributionRouteDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  description?: string | null;
  assignedDriverUserId?: string | null;
  driverName?: string | null;
  vehicleNumber?: string | null;
  vehicleCapacityKg?: number | null;
  warehouseId?: string | null;
  isActive: boolean;
  stopCount: number;
  stops: DeliveryStopDto[];
  customFields?: string | null;
  createdAt: string;
}

export interface DistributionRouteSummaryDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  driverName?: string | null;
  vehicleNumber?: string | null;
  vehicleCapacityKg?: number | null;
  warehouseId?: string | null;
  isActive: boolean;
  stopCount: number;
  createdAt: string;
}

export interface CreateRouteInput {
  legalEntityId: string;
  code: string;
  name: string;
  description?: string | null;
  assignedDriverUserId?: string | null;
  driverName?: string | null;
  vehicleNumber?: string | null;
  vehicleCapacityKg?: number | null;
  warehouseId?: string | null;
  isActive: boolean;
  stops?: DeliveryStopInput[];
  customFields?: string | null;
}

export interface TripInventoryItemDto {
  id: string;
  tripId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  uom: string;
  loadedQuantity: number;
  soldQuantity: number;
  returnedQuantity: number;
  damagedQuantity: number;
  unitPrice: number;
}

export interface TripInventoryInput {
  itemId: string;
  itemCode: string;
  itemName: string;
  uom: string;
  loadedQuantity: number;
  unitPrice: number;
}

export interface VanSalesTripSummaryDto {
  id: string;
  legalEntityId: string;
  tripNumber: string;
  routeId: string;
  routeName: string;
  driverName: string;
  vehicleNumber: string;
  departureDate: string;
  status: TripStatus;
  totalSalesAmount: number;
  totalCollectionsAmount: number;
  itemCount: number;
  createdAt: string;
}

export interface VanSalesTripDto {
  id: string;
  legalEntityId: string;
  tripNumber: string;
  routeId: string;
  routeName: string;
  driverUserId: string;
  driverName: string;
  vehicleNumber: string;
  warehouseId: string;
  departureDate: string;
  returnDate?: string | null;
  status: TripStatus;
  totalSalesAmount: number;
  totalCollectionsAmount: number;
  notes?: string | null;
  inventoryItems: TripInventoryItemDto[];
  customFields?: string | null;
  createdAt: string;
}

export interface CreateTripInput {
  legalEntityId: string;
  routeId: string;
  driverUserId: string;
  driverName: string;
  vehicleNumber: string;
  warehouseId: string;
  departureDate: string;
  notes?: string | null;
  items: TripInventoryInput[];
  customFields?: string | null;
}

export interface RecordTripItemReconciliationInput {
  itemId: string;
  soldQuantity: number;
  returnedQuantity: number;
  damagedQuantity: number;
}

export interface CompleteTripInput {
  tripId: string;
  totalSalesAmount: number;
  totalCollectionsAmount: number;
  items: RecordTripItemReconciliationInput[];
  notes?: string | null;
}

export interface WholesalePriceMatrixDto {
  id: string;
  legalEntityId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  tier: WholesaleTier;
  minQuantity: number;
  maxQuantity?: number | null;
  discountPercentage: number;
  wholesaleUnitPrice: number;
  isActive: boolean;
  notes?: string | null;
  createdAt: string;
}

export interface CreateWholesalePriceInput {
  legalEntityId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  tier: WholesaleTier;
  minQuantity: number;
  maxQuantity?: number | null;
  discountPercentage: number;
  wholesaleUnitPrice: number;
  isActive: boolean;
  notes?: string | null;
}

export interface WholesaleRateCalculationDto {
  itemId: string;
  tier: WholesaleTier;
  quantity: number;
  unitPrice: number;
  discountPercentage: number;
  extendedTotal: number;
  tierApplied: boolean;
}

export interface DistributionKpiSummaryDto {
  totalRoutes: number;
  activeRoutes: number;
  activeTripsToday: number;
  completedTripsMonth: number;
  totalVanSalesMonth: number;
  totalCollectionsMonth: number;
  wholesaleRulesCount: number;
}

@Injectable({ providedIn: 'root' })
export class DistributionRoutesApi extends CrudApiService<DistributionRouteSummaryDto, Partial<CreateRouteInput>> {
  protected readonly resource = 'm/distribution/routes';

  getDetail(id: string): Observable<DistributionRouteDto> {
    return this.http.get<DistributionRouteDto>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class DistributionTripsApi extends CrudApiService<VanSalesTripSummaryDto, Partial<CreateTripInput>> {
  protected readonly resource = 'm/distribution/trips';

  getDetail(id: string): Observable<VanSalesTripDto> {
    return this.http.get<VanSalesTripDto>(`${this.url}/${id}`);
  }

  completeTrip(id: string, input: CompleteTripInput): Observable<VanSalesTripDto> {
    return this.http.post<VanSalesTripDto>(`${this.url}/${id}/complete`, input);
  }
}

@Injectable({ providedIn: 'root' })
export class DistributionWholesaleApi extends CrudApiService<WholesalePriceMatrixDto, Partial<CreateWholesalePriceInput>> {
  protected readonly resource = 'm/distribution/wholesale';

  calculateRate(legalEntityId: string, itemId: string, tier: WholesaleTier, quantity: number): Observable<WholesaleRateCalculationDto> {
    return this.http.post<WholesaleRateCalculationDto>(`${this.url}/calculate-rate`, {
      legalEntityId,
      itemId,
      tier,
      quantity
    });
  }
}

@Injectable({ providedIn: 'root' })
export class DistributionKpiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getSummary(): Observable<DistributionKpiSummaryDto> {
    return this.http.get<DistributionKpiSummaryDto>(`${this.baseUrl}/m/distribution/kpis`);
  }
}
