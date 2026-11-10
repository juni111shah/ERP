import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** A party as its detail page shows it (the totals are optional: the cards read them from the document lists). */
export interface PartyDetailDto {
  id: string; code: string; name: string; kind: string; roles: string[]; email: string | null; phone: string | null; taxNumber: string | null; currencyCode: string | null;
  paymentTermsDays: number | null; address: string | null; isActive: boolean;
  totalSales?: number; totalPurchases?: number; outstandingReceivables?: number; outstandingPayables?: number; creditLimit?: number;
}

/** The finance document lists the party page reads its recent activity from. */
export type PartyDocumentList = 'invoices' | 'receipts' | 'bills' | 'payments';

/** One row of an invoice, receipt, bill or payment list: the fields the party page's activity table reads. */
export interface PartyDocumentRow {
  id: string; number: string; status: string; currencyCode: string; isCreditNote?: boolean;
  invoiceDate?: string; billDate?: string; date?: string; total?: number; amount?: number; paid?: number;
}

/** Open items of a customer or vendor as of a date. */
export interface PartyStatementDto {
  asOf: string;
  items: { invoiceId: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; daysOverdue: number; outstanding: number }[];
  unallocatedReceipts?: number; unallocatedPayments?: number; balance: number;
}

/** Newest first, by the date each list is sorted on. */
const RECENT_SORT: Record<PartyDocumentList, string> = { invoices: '-invoiceDate', receipts: '-date', bills: '-billDate', payments: '-date' };

/** The party detail page: the party, its latest documents and its statement. */
@Injectable({ providedIn: 'root' })
export class PartyActivityApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  party(id: string): Observable<PartyDetailDto> { return this.http.get<PartyDetailDto>(`${this.base}/parties/${id}`); }

  /** The ten latest documents of one list for the party. */
  recent(list: PartyDocumentList, partyId: string): Observable<{ items?: PartyDocumentRow[] } | null> {
    return this.http.get<{ items?: PartyDocumentRow[] } | null>(`${this.base}/finance/${list}?filter=partyId:eq:${partyId}&pageSize=10&sort=${RECENT_SORT[list]}`);
  }

  /** GET /finance/reports/{customer|vendor}-statement. */
  statement(kind: 'customer' | 'vendor', partyId: string, asOf: string): Observable<PartyStatementDto> {
    return this.http.get<PartyStatementDto>(`${this.base}/finance/reports/${kind}-statement`, { params: { partyId, asOf } });
  }
}
