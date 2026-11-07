import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudApiService } from './crud-api.service';

export type ReturnStatus = 'draft' | 'posted' | 'voided';
export type ReturnKind = 'sales' | 'purchase';

export interface SalesReturnSummaryDto {
  id: string; number: string; legalEntityId: string; salesOrderId: string | null; orderNumber: string | null; invoiceId: string | null; invoiceNumber: string | null; customerPartyId: string; customerName: string;
  returnDate: string; status: ReturnStatus; creditNoteId: string | null; creditNoteNumber: string | null; totalAmount: number; lineCount: number; createdAt: string;
}
export interface SalesReturnLineDto { id: string; orderLineId: string | null; invoiceLineId: string | null; itemId: string | null; itemCode: string; description: string; quantity: number; restock: boolean; }
export interface SalesReturnDto extends SalesReturnSummaryDto { reason: string; notes: string | null; subTotal: number; taxTotal: number; lines: SalesReturnLineDto[]; voidReason: string | null; voidedAt: string | null; }

export interface PurchaseReturnSummaryDto {
  id: string; number: string; legalEntityId: string; purchaseOrderId: string | null; purchaseOrderNumber: string | null; billId: string | null; vendorPartyId: string; vendorName: string;
  returnDate: string; status: ReturnStatus; totalAmount: number; stockValue: number; lineCount: number; createdAt: string;
}
export interface PurchaseReturnLineDto { id: string; orderLineId: string | null; billLineId: string | null; itemId: string | null; itemCode: string; description: string; quantity: number; }
export interface PurchaseReturnBillShareDto { billId: string; quantity: number; amount: number; appliedToBill: number; }
export interface PurchaseReturnDto extends PurchaseReturnSummaryDto {
  reason: string; notes: string | null; warehouseId: string | null; subTotal: number; taxTotal: number; journalEntryId: string | null; lines: PurchaseReturnLineDto[]; billShares: PurchaseReturnBillShareDto[];
  voidReason: string | null; voidedAt: string | null;
}

export type ReturnSummary = SalesReturnSummaryDto | PurchaseReturnSummaryDto;
export type ReturnDetail = SalesReturnDto | PurchaseReturnDto;

export interface SalesReturnInput {
  salesOrderId?: string | null; invoiceId?: string | null; returnDate: string; reason: string; notes?: string | null;
  lines: { orderLineId?: string | null; invoiceLineId?: string | null; quantity: number; restock: boolean }[];
}
export interface PurchaseReturnInput {
  purchaseOrderId?: string | null; billId?: string | null; returnDate: string; reason: string; notes?: string | null; warehouseId?: string | null;
  lines: { orderLineId?: string | null; billLineId?: string | null; quantity: number }[];
}

abstract class ReturnsApiBase<S, D, I> extends CrudApiService<S, I> {
  detail(id: string): Observable<D> { return this.http.get<D>(`${this.url}/${id}`); }
  post(id: string): Observable<D> { return this.http.post<D>(`${this.url}/${id}/post`, {}); }
  void(id: string, reason: string): Observable<D> { return this.http.post<D>(`${this.url}/${id}/void`, { reason }); }
}

@Injectable({ providedIn: 'root' })
export class SalesReturnsApi extends ReturnsApiBase<SalesReturnSummaryDto, SalesReturnDto, SalesReturnInput> {
  protected readonly resource = 'm/sales/returns';
}

@Injectable({ providedIn: 'root' })
export class PurchaseReturnsApi extends ReturnsApiBase<PurchaseReturnSummaryDto, PurchaseReturnDto, PurchaseReturnInput> {
  protected readonly resource = 'm/purchasing/returns';
}
