import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type PosSessionStatus = 'Open' | 'Closed';
export type CashMovementType = 'FloatIn' | 'PayIn' | 'PayOut' | 'DropOut';
export type PosOrderStatus = 'Completed' | 'Voided' | 'Parked';
export type PosPaymentStatus = 'Paid' | 'PartiallyPaid' | 'Refunded';
export type PosPaymentMethod = 'Cash' | 'Card' | 'GiftCard' | 'Account' | 'Other';

export interface PosRegisterDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  defaultCashAccountId?: string | null;
  isActive: boolean;
  receiptHeader?: string | null;
  receiptFooter?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface PosRegisterInput {
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  defaultCashAccountId?: string | null;
  isActive: boolean;
  receiptHeader?: string | null;
  receiptFooter?: string | null;
  customFields?: string | null;
}

export interface CashDrawerTransactionDto {
  id: string;
  sessionId: string;
  type: CashMovementType;
  amount: number;
  reason: string;
  performedByUserId: string;
  timestamp: string;
}

export interface PosSessionSummaryDto {
  id: string;
  legalEntityId: string;
  registerId: string;
  registerName: string;
  openedByUserId: string;
  cashierName: string;
  openedAt: string;
  closedByUserId?: string | null;
  closedAt?: string | null;
  openingCash: number;
  totalSales: number;
  expectedCash: number;
  actualCash: number;
  cashDifference: number;
  status: PosSessionStatus;
  notes?: string | null;
  createdAt: string;
}

export interface PosSessionDto extends PosSessionSummaryDto {
  totalReturns: number;
  totalPayIn: number;
  totalPayOut: number;
  customFields?: string | null;
  drawerTransactions: CashDrawerTransactionDto[];
}

export interface OpenSessionCommand {
  legalEntityId: string;
  registerId: string;
  openingCash: number;
  cashierUserId: string;
  cashierName: string;
  notes?: string | null;
}

export interface CloseSessionCommand {
  sessionId?: string;
  actualCash: number;
  closedByUserId: string;
  notes?: string | null;
}

export interface RecordDrawerTransactionCommand {
  sessionId?: string;
  type: CashMovementType;
  amount: number;
  reason: string;
  performedByUserId: string;
}

export interface PosOrderLineInput {
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
}

export interface PosPaymentInput {
  method: PosPaymentMethod;
  amount: number;
  tenderedAmount: number;
  changeAmount: number;
  reference?: string | null;
}

export interface CreatePosOrderCommand {
  legalEntityId: string;
  registerId: string;
  sessionId: string;
  customerPartyId?: string | null;
  customerName?: string | null;
  cashierUserId: string;
  cashierName: string;
  lines: PosOrderLineInput[];
  payments: PosPaymentInput[];
  notes?: string | null;
  customFields?: string | null;
}

export interface PosOrderLineDto {
  id: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
  lineTotal: number;
}

export interface PosPaymentDto {
  id: string;
  method: PosPaymentMethod;
  amount: number;
  tenderedAmount: number;
  changeAmount: number;
  reference?: string | null;
  paymentDate: string;
}

export interface PosOrderSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  registerId: string;
  sessionId: string;
  customerPartyId?: string | null;
  customerName: string;
  orderDate: string;
  subTotal: number;
  taxTotal: number;
  discountTotal: number;
  totalAmount: number;
  status: PosOrderStatus;
  paymentStatus: PosPaymentStatus;
  cashierName: string;
  lineCount: number;
  createdAt: string;
}

export interface PosOrderDto extends PosOrderSummaryDto {
  cashierUserId: string;
  notes?: string | null;
  customFields?: string | null;
  lines: PosOrderLineDto[];
  payments: PosPaymentDto[];
}

export interface PosKpiSummaryDto {
  todaySales: number;
  activeRegistersCount: number;
  openSessionsCount: number;
  totalOrdersToday: number;
  averageTicket: number;
}

@Injectable({ providedIn: 'root' })
export class PosRegistersApi extends CrudApiService<PosRegisterDto, PosRegisterInput> {
  protected readonly resource = 'm/pos/registers';

  override get(id: string): Observable<PosRegisterDto> {
    return this.http.get<PosRegisterDto>(`${this.url}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class PosSessionsApi extends CrudApiService<PosSessionSummaryDto, unknown> {
  protected readonly resource = 'm/pos/sessions';

  getCurrent(registerId: string): Observable<PosSessionDto | null> {
    return this.http.get<PosSessionDto | null>(`${this.url}/current?registerId=${registerId}`);
  }

  openSession(cmd: OpenSessionCommand): Observable<PosSessionDto> {
    return this.http.post<PosSessionDto>(`${this.url}/open`, cmd);
  }

  closeSession(id: string, cmd: CloseSessionCommand): Observable<PosSessionDto> {
    return this.http.post<PosSessionDto>(`${this.url}/${id}/close`, cmd);
  }

  recordDrawerTransaction(id: string, cmd: RecordDrawerTransactionCommand): Observable<CashDrawerTransactionDto> {
    return this.http.post<CashDrawerTransactionDto>(`${this.url}/${id}/drawer-transaction`, cmd);
  }
}

@Injectable({ providedIn: 'root' })
export class PosOrdersApi extends CrudApiService<PosOrderSummaryDto, CreatePosOrderCommand> {
  protected readonly resource = 'm/pos/orders';

  override get(id: string): Observable<PosOrderDto> {
    return this.http.get<PosOrderDto>(`${this.url}/${id}`);
  }

  voidOrder(id: string, reason: string): Observable<PosOrderDto> {
    return this.http.post<PosOrderDto>(`${this.url}/${id}/void`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class PosKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getSummary(): Observable<PosKpiSummaryDto> {
    return this.http.get<PosKpiSummaryDto>(`${this.base}/m/pos/kpis`);
  }
}
