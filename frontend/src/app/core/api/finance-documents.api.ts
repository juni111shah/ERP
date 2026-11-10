import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** Read-side views of the finance documents (detail pages, print details, lease links) and the endpoints that serve them. */

/** Issuer and party details for an invoice, read from the print endpoint (same permission as reading the invoice). */
export interface InvoiceExtras {
  header: { organizationName: string; legalEntityName: string; legalEntityAddress?: string | null; taxRegistrationNo?: string | null; phone?: string | null; email?: string | null; countryCode: string };
  partyAddress?: string | null; partyTaxNumber?: string | null; partyPhone?: string | null; partyEmail?: string | null;
  /** Set on a credit note linked to an invoice (SourceType "invoice"). */
  originalInvoiceId?: string | null; originalInvoiceNumber?: string | null;
}

/** A receipt (or other settlement) applied to an invoice. */
export interface InvoiceAllocation { settlementId: string; number: string; date: string; method: string; reference?: string | null; currencyCode: string; amount: number; status: string; }

/** Lease / property behind an invoice, supplied by the real-estate module only when it is installed (the core invoice knows nothing about leases). */
export interface InvoiceLeaseLink { invoiceId: string; leaseId: string; leaseNumber?: string | null; propertyId: string; propertyName?: string | null; unitNumber?: string | null; periodFrom: string; periodTo: string; }

/** An invoice or credit note as the detail page shows it. */
export interface InvoiceDetailDto {
  id: string; number: string; isCreditNote: boolean; legalEntityId: string; partyId: string; partyName?: string | null; invoiceDate: string; dueDate: string; currencyCode: string;
  net: number; tax: number; total: number; paid: number; status: string; sourceType?: string | null; sourceId?: string | null; creditNoteForId?: string | null; notes?: string | null;
  approvalRequestId?: string | null; journalEntryId: string | null;
  lines: { id?: string; seq?: number; description: string; quantity: number; price: number; taxCodeId?: string | null; taxAmount?: number; net?: number }[];
}

export interface BillDetailLine { id: string; seq: number; description: string; quantity: number; price: number; taxCodeId: string | null; taxAmount: number; taxRecoverable: number; net: number; accountId: string | null; }

/** A vendor bill as the detail page shows it. */
export interface BillDetailDto {
  id: string; number: string; legalEntityId: string; partyId: string; partyName: string; vendorReference: string | null; billDate: string; dueDate: string; currencyCode: string;
  net: number; tax: number; total: number; paid: number; status: string; approvalRequestId: string | null; journalEntryId: string | null; notes: string | null; lines: BillDetailLine[];
}

/** The fields a customer receipt and a vendor payment share on their detail pages. */
interface SettlementDetail {
  id: string; number: string; legalEntityId: string; partyId: string; partyName: string; date: string; method: string; bankAccountId: string | null; currencyCode: string;
  amount: number; unallocated: number; reference: string | null; status: string; journalEntryId: string | null;
}

/** A customer receipt with the invoices it settles. */
export interface ReceiptDetailDto extends SettlementDetail { allocations: { id: string; invoiceId: string; invoiceNumber: string; amount: number }[]; }

/** A vendor payment with the bills it settles. */
export interface PaymentDetailDto extends SettlementDetail { paymentInstrumentId: string | null; allocations: { id: string; billId: string; billNumber: string; amount: number }[]; }

export interface JournalDetailLine { id: string; seq: number; accountId: string; accountCode: string; accountName: string; debit: number; credit: number; description: string | null; partyId: string | null; }

/** A journal entry as the detail page shows it. */
export interface JournalDetailDto {
  id: string; number: string; legalEntityId: string; date: string; memo: string | null; reference: string | null; sourceType: string | null; sourceId: string | null; currencyCode: string;
  status: string; amount: number; reversalOfId: string | null; postedAt: string | null; lines: JournalDetailLine[];
}

/** The finance detail pages: one document each, the invoice's allocations and its print details. */
@Injectable({ providedIn: 'root' })
export class FinanceDocumentsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/finance`;
  private readonly printBase = `${inject(API_BASE_URL)}/print`;

  invoice(id: string): Observable<InvoiceDetailDto> { return this.http.get<InvoiceDetailDto>(`${this.base}/invoices/${id}`); }
  voidInvoice(id: string, reason: string): Observable<InvoiceDetailDto> { return this.http.post<InvoiceDetailDto>(`${this.base}/invoices/${id}/void`, { reason }); }
  invoiceAllocations(id: string): Observable<InvoiceAllocation[]> { return this.http.get<InvoiceAllocation[]>(`${this.base}/invoices/${id}/allocations`); }
  bill(id: string): Observable<BillDetailDto> { return this.http.get<BillDetailDto>(`${this.base}/bills/${id}`); }
  receipt(id: string): Observable<ReceiptDetailDto> { return this.http.get<ReceiptDetailDto>(`${this.base}/receipts/${id}`); }
  payment(id: string): Observable<PaymentDetailDto> { return this.http.get<PaymentDetailDto>(`${this.base}/payments/${id}`); }
  journal(id: string): Observable<JournalDetailDto> { return this.http.get<JournalDetailDto>(`${this.base}/journals/${id}`); }

  /** Never errors: a failure just means the invoice sheet shows the party name without the extra details. */
  invoiceExtras(id: string): Observable<InvoiceExtras | null> {
    return this.http.get<InvoiceExtras>(`${this.printBase}/invoice/${id}`).pipe(map((r) => r ?? null), catchError(() => of(null)));
  }
}

/** The real-estate module's billing endpoints the core invoice screens use when that module is installed. */
@Injectable({ providedIn: 'root' })
export class InvoiceLeaseLinksApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/m/realestate/billing`;

  /** The lease behind each of these invoices (only invoices the module raised have one). */
  links(ids: string[]): Observable<InvoiceLeaseLink[]> { return this.http.get<InvoiceLeaseLink[]>(`${this.base}/invoice-links`, { params: { ids } }); }
  /** Invoices every lease instalment due by `asOf`. */
  runBilling(asOf: string): Observable<{ invoiced: number }> { return this.http.post<{ invoiced: number }>(`${this.base}/run`, { asOf }); }
}
