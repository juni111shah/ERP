import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type PurchaseOrderStatus = 'Draft' | 'Submitted' | 'Approved' | 'PartiallyReceived' | 'Received' | 'Billed' | 'Cancelled';
export type PurchaseRequisitionStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Ordered';

export interface PurchaseOrderLineInput {
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
}

export interface PurchaseOrderInput {
  legalEntityId: string;
  number?: string | null;
  requisitionId?: string | null;
  vendorPartyId?: string | null;
  vendorName: string;
  vendorContact?: string | null;
  vendorReference?: string | null;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  currency: string;
  paymentTerms?: string | null;
  deliveryAddress?: string | null;
  notes?: string | null;
  lines: PurchaseOrderLineInput[];
  customFields?: Record<string, unknown> | null;
}

export interface PurchaseOrderLineDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  receivedQuantity: number;
  billedQuantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
  lineTotal: number;
}

export interface PurchaseOrderSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  vendorPartyId?: string | null;
  vendorName: string;
  vendorReference?: string | null;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  currency: string;
  status: PurchaseOrderStatus;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  totalAmount: number;
  lineCount: number;
  invoicedBillId?: string | null;
  createdAt: string;
}

export interface PurchaseOrderDto extends PurchaseOrderSummaryDto {
  requisitionId?: string | null;
  vendorContact?: string | null;
  paymentTerms?: string | null;
  deliveryAddress?: string | null;
  notes?: string | null;
  lines: PurchaseOrderLineDto[];
  customFields?: Record<string, unknown> | null;
}

export interface RequisitionLineInput {
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
}

export interface RequisitionInput {
  legalEntityId: string;
  number?: string | null;
  requesterName: string;
  department?: string | null;
  requisitionDate: string;
  requiredDate?: string | null;
  reason?: string | null;
  lines: RequisitionLineInput[];
  customFields?: Record<string, unknown> | null;
}
export type PurchaseRequisitionInput = RequisitionInput;

export interface RequisitionLineDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
  lineTotal: number;
}

export interface RequisitionSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  requesterName: string;
  department?: string | null;
  requisitionDate: string;
  requiredDate?: string | null;
  status: PurchaseRequisitionStatus;
  estimatedTotal: number;
  lineCount: number;
  convertedPurchaseOrderId?: string | null;
  createdAt: string;
}

export interface RequisitionDto extends RequisitionSummaryDto {
  reason?: string | null;
  lines: RequisitionLineDto[];
  customFields?: Record<string, unknown> | null;
}

export interface GoodsReceiptLineInput {
  purchaseOrderLineId: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantityReceived: number;
  unitOfMeasure: string;
}

export interface GoodsReceiptInput {
  legalEntityId: string;
  number?: string | null;
  purchaseOrderId: string;
  receivedBy: string;
  receiptDate: string;
  deliveryNoteNumber?: string | null;
  notes?: string | null;
  lines: GoodsReceiptLineInput[];
  customFields?: Record<string, unknown> | null;
}

export interface GoodsReceiptLineDto {
  id: string;
  purchaseOrderLineId: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantityReceived: number;
  unitOfMeasure: string;
}

export interface GoodsReceiptSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  purchaseOrderId: string;
  purchaseOrderNumber: string;
  vendorName: string;
  receiptDate: string;
  receivedBy: string;
  deliveryNoteNumber?: string | null;
  lineCount: number;
  createdAt: string;
}

export interface GoodsReceiptDto extends GoodsReceiptSummaryDto {
  notes?: string | null;
  lines: GoodsReceiptLineDto[];
  customFields?: Record<string, unknown> | null;
}

export interface PurchasingKpiDto {
  totalSpend: number;
  openOrdersAmount: number;
  openOrdersCount: number;
  pendingReceiptsCount: number;
  openRequisitionsCount: number;
  totalOrdersCount: number;
}

@Injectable({ providedIn: 'root' })
export class PurchaseOrdersApi extends CrudApiService<PurchaseOrderSummaryDto, PurchaseOrderInput> {
  protected readonly resource = 'm/purchasing/orders';

  override get(id: string): Observable<PurchaseOrderDto> {
    return this.http.get<PurchaseOrderDto>(`${this.url}/${id}`);
  }

  setStatus(id: string, status: PurchaseOrderStatus): Observable<PurchaseOrderDto> {
    return this.http.put<PurchaseOrderDto>(`${this.url}/${id}/status`, { status });
  }

  convertToBill(id: string): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/convert-to-bill`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class PurchaseRequisitionsApi extends CrudApiService<RequisitionSummaryDto, RequisitionInput> {
  protected readonly resource = 'm/purchasing/requisitions';

  override get(id: string): Observable<RequisitionDto> {
    return this.http.get<RequisitionDto>(`${this.url}/${id}`);
  }

  setStatus(id: string, status: PurchaseRequisitionStatus): Observable<RequisitionDto> {
    return this.http.put<RequisitionDto>(`${this.url}/${id}/status`, { status });
  }

  convertToOrder(id: string, vendorPartyId: string | null, vendorName: string): Observable<PurchaseOrderDto> {
    return this.http.post<PurchaseOrderDto>(`${this.url}/${id}/convert-to-order`, { vendorPartyId, vendorName });
  }
}

@Injectable({ providedIn: 'root' })
export class GoodsReceiptsApi extends CrudApiService<GoodsReceiptSummaryDto, GoodsReceiptInput> {
  protected readonly resource = 'm/purchasing/receipts';

  override get(id: string): Observable<GoodsReceiptDto> {
    return this.http.get<GoodsReceiptDto>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class PurchasingKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getSummary(): Observable<PurchasingKpiDto> {
    return this.http.get<PurchasingKpiDto>(`${this.base}/m/purchasing/kpis`);
  }
}
