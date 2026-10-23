import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';

export interface PortalInvoice { id: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; currencyCode: string; total: number; paid: number; outstanding: number; status: string; }
export interface PortalReceipt { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; reference?: string; }
export interface PortalStatement { asOf: string; items: { number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; daysOverdue: number; outstanding: number }[]; unallocatedReceipts: number; balance: number; }
export interface PortalLease { id: string; number?: string; property: string; unit: string; startDate: string; endDate: string; annualRent: number; currencyCode: string; status: string; paymentFrequency: number; securityDeposit: number; schedule: { sequence: number; dueDate: string; amount: number; state: string }[]; }
export interface PortalRequest { id: string; number?: string; leaseId?: string; issue: string; category?: string; priority: string; status: string; reportedOn: string; scheduledFor?: string; completedOn?: string; }

const KEY = 'erp.portal.session';

/** Tenant-portal session and API client. The portal token lives in sessionStorage only and is sent explicitly (it never touches the staff session). */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly session = signal<{ token: string; name: string } | null>(this.load());
  readonly signedIn = computed(() => !!this.session());
  readonly name = computed(() => this.session()?.name ?? '');

  private load(): { token: string; name: string } | null {
    try { return JSON.parse(sessionStorage.getItem(KEY) ?? 'null'); } catch { return null; }
  }

  private headers(): HttpHeaders { return new HttpHeaders({ Authorization: `Bearer ${this.session()?.token ?? ''}` }); }
  private get<T>(path: string): Observable<T> { return this.http.get<T>(`${this.base}${path}`, { headers: this.headers() }); }

  requestCode(workspace: string, email: string): Observable<unknown> { return this.http.post(`${this.base}/portal/auth/request-code`, { workspace, email }); }

  verify(workspace: string, email: string, code: string): Observable<unknown> {
    return this.http.post<{ accessToken: string; partyName: string }>(`${this.base}/portal/auth/verify`, { workspace, email, code }).pipe(tap((r) => {
      const value = { token: r.accessToken, name: r.partyName };
      try { sessionStorage.setItem(KEY, JSON.stringify(value)); } catch { /* private mode: keep it in memory only */ }
      this.session.set(value);
    }));
  }

  signOut(): void { try { sessionStorage.removeItem(KEY); } catch { /* ignore */ } this.session.set(null); }

  invoices(): Observable<PortalInvoice[]> { return this.get('/portal/invoices'); }
  receipts(): Observable<PortalReceipt[]> { return this.get('/portal/receipts'); }
  statement(): Observable<PortalStatement> { return this.get('/portal/statement'); }
  leases(): Observable<PortalLease[]> { return this.get('/m/realestate/portal/leases'); }
  requests(): Observable<PortalRequest[]> { return this.get('/m/realestate/portal/requests'); }
  raise(body: { leaseId: string; issue: string; category?: string; priority: string }): Observable<PortalRequest> {
    return this.http.post<PortalRequest>(`${this.base}/m/realestate/portal/requests`, body, { headers: this.headers() });
  }
}
