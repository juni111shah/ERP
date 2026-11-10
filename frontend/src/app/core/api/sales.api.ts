import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

/** The sales module's quotes, sales orders and KPIs. */

export type SalesQuoteStatus = 'draft' | 'sent' | 'accepted' | 'declined' | 'expired' | 'converted';
export type SalesOrderStatus = 'draft' | 'confirmed' | 'processing' | 'delivered' | 'invoiced' | 'cancelled';

/** A quote or sales-order line as the API takes it. */
export interface SalesLineInput {
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
}

export type QuoteLineInput = SalesLineInput;

export interface QuoteInput {
  legalEntityId: string;
  number?: string | null;
  customerPartyId?: string | null;
  customerName: string;
  customerEmail?: string | null;
  customerPhone?: string | null;
  quoteDate: string;
  validUntil?: string | null;
  currency: string;
  notes?: string | null;
  terms?: string | null;
  lines: QuoteLineInput[];
  customFields?: Record<string, unknown> | null;
}

export interface QuoteLineDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
  lineTotal: number;
}

export interface QuoteSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  customerPartyId?: string | null;
  customerName: string;
  quoteDate: string;
  validUntil?: string | null;
  currency: string;
  status: SalesQuoteStatus;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  totalAmount: number;
  lineCount: number;
  convertedSalesOrderId?: string | null;
  createdAt: string;
}

export interface QuoteDto extends QuoteSummaryDto {
  customerEmail?: string | null;
  customerPhone?: string | null;
  notes?: string | null;
  terms?: string | null;
  lines: QuoteLineDto[];
  customFields?: Record<string, unknown> | null;
}

export type OrderLineInput = SalesLineInput;

export interface OrderInput {
  legalEntityId: string;
  number?: string | null;
  quoteId?: string | null;
  customerPartyId?: string | null;
  customerName: string;
  customerReference?: string | null;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  currency: string;
  paymentTerms?: string | null;
  shippingAddress?: string | null;
  notes?: string | null;
  lines: OrderLineInput[];
  customFields?: Record<string, unknown> | null;
}

export interface OrderLineDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  deliveredQuantity: number;
  invoicedQuantity: number;
  returnedQuantity?: number;
  unitOfMeasure: string;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
  lineTotal: number;
}

export interface OrderSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  quoteId?: string | null;
  quoteNumber?: string | null;
  customerPartyId?: string | null;
  customerName: string;
  customerReference?: string | null;
  orderDate: string;
  expectedDeliveryDate?: string | null;
  currency: string;
  status: SalesOrderStatus;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  totalAmount: number;
  lineCount: number;
  invoicedInvoiceId?: string | null;
  createdAt: string;
}

export interface OrderDto extends OrderSummaryDto {
  paymentTerms?: string | null;
  shippingAddress?: string | null;
  notes?: string | null;
  warnings?: string[] | null;
  lines: OrderLineDto[];
  customFields?: Record<string, unknown> | null;
}

export interface OrderFulfillmentLineInput {
  lineId: string;
  deliveredQuantity: number;
}

export interface SalesKpiDto {
  totalSales: number;
  openOrdersAmount: number;
  openOrdersCount: number;
  openQuotesAmount: number;
  openQuotesCount: number;
  conversionRate: number;
  totalQuotesCount: number;
  totalOrdersCount: number;
}

/** Sales quotes: CRUD, status changes and converting a quote into a sales order. */
@Injectable({ providedIn: 'root' })
export class SalesQuotesApi extends CrudApiService<QuoteSummaryDto, QuoteInput> {
  protected readonly resource = 'm/sales/quotes';

  override get(id: string): Observable<QuoteDto> {
    return this.http.get<QuoteDto>(`${this.url}/${id}`);
  }

  setStatus(id: string, status: SalesQuoteStatus): Observable<QuoteDto> {
    return this.http.put<QuoteDto>(`${this.url}/${id}/status`, { status });
  }

  convertToOrder(id: string): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.url}/${id}/convert-to-order`, {});
  }
}

/** Sales orders: CRUD, status changes, recording deliveries and invoicing. */
@Injectable({ providedIn: 'root' })
export class SalesOrdersApi extends CrudApiService<OrderSummaryDto, OrderInput> {
  protected readonly resource = 'm/sales/orders';

  override get(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.url}/${id}`);
  }

  setStatus(id: string, status: SalesOrderStatus): Observable<OrderDto> {
    return this.http.put<OrderDto>(`${this.url}/${id}/status`, { status });
  }

  updateFulfillment(id: string, lines: OrderFulfillmentLineInput[], extra: { deliveryDate?: string | null; warehouseId?: string | null } = {}): Observable<OrderDto> {
    return this.http.put<OrderDto>(`${this.url}/${id}/fulfillment`, { lines, ...extra });
  }

  convertToInvoice(id: string): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/convert-to-invoice`, {});
  }
}

/** The sales KPI summary (totals, open quotes and orders, conversion rate). */
@Injectable({ providedIn: 'root' })
export class SalesKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getSummary(): Observable<SalesKpiDto> {
    return this.http.get<SalesKpiDto>(`${this.base}/m/sales/kpis`);
  }
}
