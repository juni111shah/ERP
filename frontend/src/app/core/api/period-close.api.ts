import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudApiService } from './crud-api.service';

export interface FiscalYearCloseDto {
  id: string; legalEntityId: string; fiscalYear: number; yearStart: string; yearEnd: string; journalEntryId: string | null; netResult: number; previousBooksLockedTo: string | null;
  closedAt: string; closedBy: string | null; reopenedAt: string | null; reopenedBy: string | null; reopenReason: string | null; reversalJournalId: string | null; alreadyClosed: boolean;
}

export interface OpeningBalanceLine {
  accountId: string; debit: number; credit: number; partyId?: string | null; description?: string | null; businessUnitId?: string | null; dueDate?: string | null; reference?: string | null;
}
export interface OpeningBalanceInput { legalEntityId: string; date: string; memo?: string | null; lines: OpeningBalanceLine[]; }
export interface OpeningBalanceDto {
  journalId: string; number: string; legalEntityId: string; date: string; status: string; amount: number; openingEquityAmount: number; invoiceIds: string[]; billIds: string[]; reversalJournalId: string | null;
}

/** Closing a fiscal year (moves income and expense to retained earnings and locks the books) and reopening the latest closed one. */
@Injectable({ providedIn: 'root' })
export class YearEndApi extends CrudApiService<FiscalYearCloseDto, never> {
  protected readonly resource = 'finance/year-end';
  close(legalEntityId: string, fiscalYear: number): Observable<FiscalYearCloseDto> { return this.http.post<FiscalYearCloseDto>(`${this.url}/close`, { legalEntityId, fiscalYear }); }
  reopen(legalEntityId: string, fiscalYear: number, reason: string): Observable<FiscalYearCloseDto> { return this.http.post<FiscalYearCloseDto>(`${this.url}/reopen`, { legalEntityId, fiscalYear, reason }); }
}

/** The balances a business starts the books with. Receivable and payable lines raise open invoices and bills. */
@Injectable({ providedIn: 'root' })
export class OpeningBalancesApi extends CrudApiService<never, never> {
  protected readonly resource = 'finance/opening-balances';
  post(body: OpeningBalanceInput): Observable<OpeningBalanceDto> { return this.http.post<OpeningBalanceDto>(this.url, body); }
  reverse(journalId: string, reason: string, date?: string | null): Observable<unknown> { return this.http.post<unknown>(`${this.url}/${journalId}/reverse`, { reason, date: date ?? null }); }
}
