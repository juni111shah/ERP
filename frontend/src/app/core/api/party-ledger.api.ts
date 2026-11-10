import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

export type PartyLedgerSide = 'customer' | 'vendor';
export interface PartyLedgerLine { date: string; kind: 'invoice' | 'creditNote' | 'receipt' | 'bill' | 'payment' | string; documentId: string; number: string; reference: string | null; currencyCode: string; debit: number; credit: number; balance: number }
export interface PartyLedger { partyId: string; partyName: string | null; side: PartyLedgerSide; from: string; to: string; openingBalance: number; lines: PartyLedgerLine[]; totalDebit: number; totalCredit: number; closingBalance: number }

/** A party's statement of account with a running balance (/api/v1/finance/reports/{customer|vendor}-ledger). */
@Injectable({ providedIn: 'root' })
export class PartyLedgerApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** `from` and `to` are calendar days ("2026-01-01"). */
  ledger(side: PartyLedgerSide, partyId: string, from: string, to: string): Observable<PartyLedger> {
    return this.http.get<PartyLedger>(`${this.base}/finance/reports/${side}-ledger`, { params: { partyId, from, to } });
  }
}
