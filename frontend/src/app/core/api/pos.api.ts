import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type PosSessionStatus = 'open' | 'closed';
export type CashMovementType = 'floatIn' | 'payIn' | 'payOut' | 'dropOut';
export type PosOrderStatus = 'completed' | 'voided' | 'parked';
export type PosPaymentStatus = 'paid' | 'partiallyPaid' | 'refunded' | 'partiallyRefunded' | 'onAccount' | 'unpaid';
export type PosPaymentMethod = 'cash' | 'card' | 'giftCard' | 'account' | 'other';

export interface PosRegisterDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  defaultCashAccountId?: string | null;
  cardAccountId?: string | null;
  varianceAccountId?: string | null;
  expenseAccountId?: string | null;
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
  /** Left out keeps the current account; the empty GUID clears it. */
  cardAccountId?: string | null;
  varianceAccountId?: string | null;
  expenseAccountId?: string | null;
  isActive: boolean;
  receiptHeader?: string | null;
  receiptFooter?: string | null;
  customFields?: string | null;
}

export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export interface CashDrawerTransactionDto {
  id: string;
  sessionId: string;
  type: CashMovementType;
  amount: number;
  reason: string;
  performedByUserId: string;
  performedByName?: string | null;
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
  totalReturns: number;
  totalPayIn: number;
  totalPayOut: number;
  expectedCash: number;
  actualCash: number;
  cashDifference: number;
  status: PosSessionStatus;
  notes?: string | null;
  createdAt: string;
}

export interface PosSessionDto extends PosSessionSummaryDto {
  customFields?: string | null;
  drawerTransactions: CashDrawerTransactionDto[];
}

export interface OpenSessionCommand {
  legalEntityId: string;
  registerId: string;
  openingCash: number;
  notes?: string | null;
}

export interface CloseSessionCommand {
  sessionId?: string;
  actualCash: number;
  notes?: string | null;
}

export interface RecordDrawerTransactionCommand {
  sessionId?: string;
  type: CashMovementType;
  amount: number;
  reason: string;
  accountId?: string | null;
}

export interface PosOrderLineInput {
  itemId?: string | null;
  itemCode?: string | null;
  description?: string | null;
  quantity: number;
  unitPrice?: number | null;
  discountPercent?: number;
  taxRate?: number | null;
}

export interface PosPaymentInput {
  method: PosPaymentMethod;
  amount: number;
  tenderedAmount: number;
  changeAmount: number;
  reference?: string | null;
}

/** What a ticket would cost with a promo code and quantity breaks, worked out by the server. Nothing is saved or used up. */
export interface PosQuoteDto {
  subTotal: number; taxTotal: number; discountTotal: number; totalAmount: number; promoCode: string | null; promoName: string | null; promoDiscount: number;
  lines: PosQuoteLineDto[];
}

/** One line of a {@link PosQuoteDto}: the price, discount and promo share the server would charge. */
export interface PosQuoteLineDto {
  itemId?: string | null; itemCode: string; description: string; quantity: number; unitPrice: number; discountPercent: number; promoDiscount: number; taxRate: number; lineTotal: number;
}

/** The ticket the till asks a quote for. */
export interface PosQuoteCommand {
  legalEntityId: string;
  customerPartyId?: string | null;
  lines: PosOrderLineInput[];
  promoCode?: string | null;
}

export interface CreatePosOrderCommand {
  legalEntityId: string;
  registerId: string;
  sessionId: string;
  customerPartyId?: string | null;
  customerName?: string | null;
  cashierUserId?: string | null;
  cashierName?: string | null;
  lines: PosOrderLineInput[];
  payments: PosPaymentInput[];
  notes?: string | null;
  customFields?: string | null;
  parkedOrderId?: string | null;
  /** A discount code the customer gave. The server checks it again; a bad code is refused. */
  promoCode?: string | null;
}

export interface ParkPosOrderCommand {
  sessionId: string;
  customerPartyId?: string | null;
  customerName?: string | null;
  lines: PosOrderLineInput[];
  notes?: string | null;
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
  returnedQuantity: number;
  returnedAmount: number;
  /** This line's share of a promo code discount. */
  promoDiscount?: number;
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
  returnedTotal: number;
  invoiceNumber?: string | null;
  status: PosOrderStatus;
  paymentStatus: PosPaymentStatus;
  cashierName: string;
  lineCount: number;
  createdAt: string;
}

export interface PosOrderDto extends PosOrderSummaryDto {
  /** The discount code used on the sale and what it took off. */
  promoCode?: string | null;
  promoDiscount?: number;
  cashierUserId: string;
  notes?: string | null;
  customFields?: string | null;
  invoiceId?: string | null;
  lines: PosOrderLineDto[];
  payments: PosPaymentDto[];
  /** What can still go back in cash, card or other tenders. */
  moneyLeftToRefund: number;
  /** What is still owing on the customer's account for this sale. */
  onAccountLeft: number;
}

export interface PosReturnLineInput {
  orderLineId: string;
  quantity: number;
  restock: boolean;
}

export interface PosRefundInput {
  method: PosPaymentMethod;
  amount: number;
  reference?: string | null;
}

export interface CreatePosReturnCommand {
  orderId: string;
  sessionId: string;
  reason: string;
  lines: PosReturnLineInput[];
  refunds: PosRefundInput[];
}

export interface PosReturnLineDto {
  id: string;
  orderLineId: string;
  itemId?: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
  netAmount: number;
  taxAmount: number;
  lineTotal: number;
  restock: boolean;
}

export interface PosReturnSummaryDto {
  id: string;
  number: string;
  legalEntityId: string;
  originalOrderId: string;
  originalNumber: string;
  registerId: string;
  sessionId: string;
  customerName: string;
  returnDate: string;
  subTotal: number;
  taxTotal: number;
  totalAmount: number;
  reason: string;
  cashierName: string;
  creditNoteNumber?: string | null;
}

export interface PosReturnDto extends PosReturnSummaryDto {
  customerPartyId?: string | null;
  creditNoteId?: string | null;
  lines: PosReturnLineDto[];
  refunds: { id: string; method: PosPaymentMethod; amount: number; reference?: string | null }[];
}

export interface PosKpiSummaryDto {
  todaySales: number;
  todayReturns: number;
  activeRegistersCount: number;
  openSessionsCount: number;
  totalOrdersToday: number;
  averageTicket: number;
}

export interface PosTerminalRegisterDto {
  id: string;
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId?: string | null;
  receiptHeader?: string | null;
  receiptFooter?: string | null;
  openSessionId?: string | null;
}

export interface PosTerminalSettingsDto {
  maxDiscountPercent: number;
  cashierDiscountPercent?: number | null;
  allowPartialPayment: boolean;
  allowNegativeDrawer: boolean;
  allowNegativeStock: boolean;
  enforceFloatAtOpen: boolean;
}

export interface PosTerminalDto {
  registers: PosTerminalRegisterDto[];
  settings: PosTerminalSettingsDto;
}

export interface PosCatalogItemDto {
  id: string;
  sku: string;
  barcode?: string | null;
  name: string;
  uom: string;
  type: string;
  /** Null when the item has no sales price: it cannot be sold from the till until it has one. */
  price: number | null;
  taxRate: number;
  /** Null when the item is not stock-tracked here. */
  onHand: number | null;
}

/** What the till looks up in the catalogue: a search text, or an exact barcode / SKU. Empty values are left out of the request. */
export interface PosCatalogQuery {
  registerId?: string;
  search?: string;
  code?: string;
  pageSize?: number;
}

/** One page of the till's catalogue and how many items match in all. */
export interface PosCatalogPage {
  items: PosCatalogItemDto[];
  total: number;
}

export interface PosCustomerDto {
  id: string;
  code: string;
  name: string;
  phone?: string | null;
  email?: string | null;
  taxNumber?: string | null;
}

export interface PosTenderRowDto {
  method: PosPaymentMethod;
  sales: number;
  refunds: number;
  net: number;
  count: number;
}

export interface PosTopItemDto {
  itemCode: string;
  description: string;
  quantity: number;
  revenue: number;
}

export interface PosShiftReportDto {
  session: PosSessionDto;
  grossSales: number;
  discounts: number;
  netSales: number;
  tax: number;
  salesTotal: number;
  returns: number;
  voids: number;
  netTakings: number;
  saleCount: number;
  voidCount: number;
  returnCount: number;
  itemsSold: number;
  averageTicket: number;
  cashSales: number;
  cashRefunds: number;
  heldCount: number;
  tenders: PosTenderRowDto[];
  topItems: PosTopItemDto[];
}

export interface PosSalesGroupDto {
  name: string;
  sales: number;
  returns: number;
  net: number;
  count: number;
}

export interface PosSalesReportDto {
  from: string;
  to: string;
  grossSales: number;
  discounts: number;
  netSales: number;
  tax: number;
  salesTotal: number;
  returns: number;
  netTakings: number;
  saleCount: number;
  voidCount: number;
  voidTotal: number;
  returnCount: number;
  averageTicket: number;
  cashOverShort: number;
  days: { date: string; sales: number; returns: number; net: number; count: number }[];
  hours: { hour: number; sales: number; count: number }[];
  tenders: PosTenderRowDto[];
  registers: PosSalesGroupDto[];
  cashiers: PosSalesGroupDto[];
  topItems: PosTopItemDto[];
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

  report(id: string): Observable<PosShiftReportDto> {
    return this.http.get<PosShiftReportDto>(`${this.url}/${id}/report`);
  }

  openSession(cmd: OpenSessionCommand): Observable<PosSessionDto> {
    return this.http.post<PosSessionDto>(`${this.url}/open`, cmd);
  }

  closeSession(id: string, cmd: CloseSessionCommand): Observable<PosSessionDto> {
    return this.http.post<PosSessionDto>(`${this.url}/${id}/close`, cmd);
  }

  recordDrawerTransaction(id: string, cmd: RecordDrawerTransactionCommand, idempotencyKey?: string): Observable<CashDrawerTransactionDto> {
    return this.http.post<CashDrawerTransactionDto>(`${this.url}/${id}/drawer-transaction`, cmd, idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {});
  }
}

@Injectable({ providedIn: 'root' })
export class PosOrdersApi extends CrudApiService<PosOrderSummaryDto, CreatePosOrderCommand> {
  protected readonly resource = 'm/pos/orders';

  override get(id: string): Observable<PosOrderDto> {
    return this.http.get<PosOrderDto>(`${this.url}/${id}`);
  }

  byNumber(number: string): Observable<PosOrderDto> {
    return this.http.get<PosOrderDto>(`${this.url}/by-number/${encodeURIComponent(number)}`);
  }

  /** The key stays the same for every retry of one sale, so a response lost on the network cannot charge the customer twice. */
  sell(cmd: CreatePosOrderCommand, idempotencyKey: string): Observable<PosOrderDto> {
    return this.http.post<PosOrderDto>(this.url, cmd, { headers: { 'Idempotency-Key': idempotencyKey } });
  }

  park(cmd: ParkPosOrderCommand): Observable<PosOrderDto> {
    return this.http.post<PosOrderDto>(`${this.url}/park`, cmd);
  }

  discardParked(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}/park`);
  }

  quote(cmd: PosQuoteCommand): Observable<PosQuoteDto> {
    return this.http.post<PosQuoteDto>(`${this.url}/quote`, cmd);
  }

  voidOrder(id: string, reason: string): Observable<PosOrderDto> {
    return this.http.post<PosOrderDto>(`${this.url}/${id}/void`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class PosReturnsApi extends CrudApiService<PosReturnSummaryDto, CreatePosReturnCommand> {
  protected readonly resource = 'm/pos/returns';

  override get(id: string): Observable<PosReturnDto> {
    return this.http.get<PosReturnDto>(`${this.url}/${id}`);
  }

  submit(cmd: CreatePosReturnCommand, idempotencyKey: string): Observable<PosReturnDto> {
    return this.http.post<PosReturnDto>(this.url, cmd, { headers: { 'Idempotency-Key': idempotencyKey } });
  }
}

/** What the till reads. These need only the right to sell. */
@Injectable({ providedIn: 'root' })
export class PosTerminalApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  private get url(): string {
    return `${this.base}/m/pos/terminal`;
  }

  bootstrap(): Observable<PosTerminalDto> {
    return this.http.get<PosTerminalDto>(this.url);
  }

  session(registerId: string): Observable<PosSessionDto | null> {
    return this.http.get<PosSessionDto | null>(`${this.url}/session`, { params: { registerId } });
  }

  catalog(query: PosCatalogQuery): Observable<PosCatalogPage> {
    const params = Object.fromEntries(Object.entries(query).filter(([, v]) => v !== undefined && v !== '')) as Record<string, string | number>;
    return this.http.get<PosCatalogPage>(`${this.url}/catalog`, { params });
  }

  customers(search: string): Observable<PosCustomerDto[]> {
    return this.http.get<PosCustomerDto[]>(`${this.url}/customers`, { params: search ? { search } : {} });
  }

  held(registerId: string): Observable<PosOrderDto[]> {
    return this.http.get<PosOrderDto[]>(`${this.url}/held`, { params: { registerId } });
  }
}

@Injectable({ providedIn: 'root' })
export class PosKpiApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  getSummary(): Observable<PosKpiSummaryDto> {
    return this.http.get<PosKpiSummaryDto>(`${this.base}/m/pos/kpis`);
  }

  /** Without dates the server reports the shop's own today, in the workspace's time zone. */
  salesReport(from?: string, to?: string, registerId?: string | null): Observable<PosSalesReportDto> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from;
    if (to) params['to'] = to;
    if (registerId) params['registerId'] = registerId;
    return this.http.get<PosSalesReportDto>(`${this.base}/m/pos/reports/sales`, { params });
  }
}
