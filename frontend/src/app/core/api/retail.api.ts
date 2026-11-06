import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type PriceListType = 'standard' | 'promotional' | 'tiered' | 'member';
export type PromotionType = 'percentageDiscount' | 'fixedAmountDiscount' | 'buyXGetY' | 'bundle';

export interface RetailStoreDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  storeManagerName?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  isActive: boolean;
  customFields?: string | null;
  createdAt: string;
}

export interface RetailStoreInput {
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  storeManagerName?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  isActive: boolean;
  customFields?: string | null;
}

export interface RetailPriceListItemDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  itemName: string;
  basePrice: number;
  price: number;
  minQuantity?: number | null;
}

export interface RetailPriceListItemInput {
  itemId?: string | null;
  itemCode: string;
  itemName: string;
  basePrice: number;
  price: number;
  minQuantity?: number | null;
}

export interface RetailPriceListSummaryDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  validFrom?: string | null;
  validTo?: string | null;
  isDefault: boolean;
  isActive: boolean;
  itemCount: number;
  createdAt: string;
}

export interface RetailPriceListDetailDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  validFrom?: string | null;
  validTo?: string | null;
  isDefault: boolean;
  isActive: boolean;
  description?: string | null;
  items: RetailPriceListItemDto[];
  customFields?: string | null;
  createdAt: string;
}

export interface CreatePriceListInput {
  legalEntityId: string;
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  validFrom?: string | null;
  validTo?: string | null;
  isDefault: boolean;
  isActive: boolean;
  description?: string | null;
  items: RetailPriceListItemInput[];
  customFields?: string | null;
}

export interface RetailPromotionDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  type: PromotionType;
  discountValue: number;
  minimumOrderAmount?: number | null;
  startDate: string;
  endDate: string;
  usageLimit?: number | null;
  timesUsed: number;
  isActive: boolean;
  description?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface CreatePromotionInput {
  legalEntityId: string;
  code: string;
  name: string;
  type: PromotionType;
  discountValue: number;
  minimumOrderAmount?: number | null;
  startDate: string;
  endDate: string;
  usageLimit?: number | null;
  isActive: boolean;
  description?: string | null;
  customFields?: string | null;
}

export interface PromoValidationResultDto {
  isValid: boolean;
  message?: string;
  type?: PromotionType | null;
  discountValue: number;
  calculatedDiscount: number;
}

export interface RetailKpiSummaryDto {
  totalStoresCount: number;
  activeStoresCount: number;
  activePriceListsCount: number;
  activePromotionsCount: number;
  totalPromotionsRedeemed: number;
}

@Injectable({ providedIn: 'root' })
export class RetailStoresApi extends CrudApiService<RetailStoreDto, Partial<RetailStoreInput>> {
  protected readonly resource = 'm/retail/stores';
}

@Injectable({ providedIn: 'root' })
export class RetailPriceListsApi extends CrudApiService<RetailPriceListSummaryDto, Partial<CreatePriceListInput>> {
  protected readonly resource = 'm/retail/pricelists';

  getDetail(id: string): Observable<RetailPriceListDetailDto> {
    return this.http.get<RetailPriceListDetailDto>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class RetailPromotionsApi extends CrudApiService<RetailPromotionDto, Partial<CreatePromotionInput>> {
  protected readonly resource = 'm/retail/promotions';

  validateCode(legalEntityId: string, promoCode: string, orderTotal: number): Observable<PromoValidationResultDto> {
    return this.http.post<PromoValidationResultDto>(`${this.url}/validate`, {
      legalEntityId,
      promoCode,
      orderTotal
    });
  }
}

@Injectable({ providedIn: 'root' })
export class RetailKpiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getSummary(): Observable<RetailKpiSummaryDto> {
    return this.http.get<RetailKpiSummaryDto>(`${this.baseUrl}/m/retail/kpis`);
  }
}
