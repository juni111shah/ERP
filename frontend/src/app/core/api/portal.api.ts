import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './api-config';

// ---- Customer (tenant) portal ----------------------------------------------------------------------------------------------

export interface PortalInvoice { id: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; currencyCode: string; total: number; paid: number; outstanding: number; status: string; }
export interface PortalReceipt { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; reference?: string; }
export interface PortalStatement { asOf: string; items: { number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; daysOverdue: number; outstanding: number }[]; unallocatedReceipts: number; balance: number; }
export interface PortalLease { id: string; number?: string; property: string; unit: string; startDate: string; endDate: string; annualRent: number; currencyCode: string; status: string; paymentFrequency: number; securityDeposit: number; schedule: { sequence: number; dueDate: string; amount: number; state: string }[]; }
export interface PortalRequest { id: string; number?: string; leaseId?: string; issue: string; category?: string; priority: string; status: string; reportedOn: string; scheduledFor?: string; completedOn?: string; }
export interface PortalRequestInput { leaseId: string; issue: string; category?: string; priority: string; }

export interface PortalMilestone { id: string; title: string; dueDate: string; status: string; achievedDate?: string | null; }
export interface PortalProject { id: string; number: string; name: string; status: string; startDate?: string | null; endDate?: string | null; percentComplete: number; managerName?: string | null; milestones: PortalMilestone[]; }
export interface PortalAcceptance { id: string; projectId: string; projectName: string; title: string; status: string; requestedAt: string; signedByName?: string | null; signedAt?: string | null; }
export interface PortalSignInput { accept: boolean; signedByName: string; signature?: string | null; comment?: string | null; signatureImage?: string | null; }

// ---- Vendor portal ---------------------------------------------------------------------------------------------------------

export interface VendorBill { id: string; number: string; vendorReference?: string; billDate: string; dueDate: string; currencyCode: string; total: number; paid: number; outstanding: number; status: string; }
export interface VendorPayment { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; reference?: string; }
export interface VendorStatement { asOf: string; items: { number: string; billDate: string; dueDate: string; daysOverdue: number; outstanding: number }[]; balance: number; }
export interface VendorWorkOrder { id: string; number?: string; issue: string; category?: string; priority: string; status: string; reportedOn: string; scheduledFor?: string; completedOn?: string; estimatedCost: number; actualCost?: number; resolutionNotes?: string; }
export interface VendorWorkOrderUpdate { status: string; actualCost?: number | null; notes?: string | null; }

/** The signed-in portal party, as kept in sessionStorage. */
interface PortalSession { token: string; name: string; kind?: string; }

const KEY = 'erp.portal.session';

/** Tenant-portal session and API client. The portal token lives in sessionStorage only and is sent explicitly (it never touches the staff session). */
@Injectable({ providedIn: 'root' })
export class PortalService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly session = signal<PortalSession | null>(this.load());
  readonly signedIn = computed(() => !!this.session());
  readonly name = computed(() => this.session()?.name ?? '');
  readonly kind = computed(() => this.session()?.kind ?? 'customer');

  // ---- session ----

  requestCode(workspace: string, email: string, kind?: string): Observable<unknown> { return this.http.post(`${this.base}/portal/auth/request-code`, { workspace, email, kind }); }

  verify(workspace: string, email: string, code: string, kind?: string): Observable<{ kind: string }> {
    return this.http.post<{ accessToken: string; partyName: string; kind: string }>(`${this.base}/portal/auth/verify`, { workspace, email, code, kind }).pipe(tap((r) => {
      const value: PortalSession = { token: r.accessToken, name: r.partyName, kind: r.kind };
      try { sessionStorage.setItem(KEY, JSON.stringify(value)); } catch { /* private mode: keep it in memory only */ }
      this.session.set(value);
    }));
  }

  signOut(): void { try { sessionStorage.removeItem(KEY); } catch { /* ignore */ } this.session.set(null); }

  // ---- customer ----

  invoices(): Observable<PortalInvoice[]> { return this.get('/portal/invoices'); }
  receipts(): Observable<PortalReceipt[]> { return this.get('/portal/receipts'); }
  statement(): Observable<PortalStatement> { return this.get('/portal/statement'); }
  leases(): Observable<PortalLease[]> { return this.get('/m/realestate/portal/leases'); }
  requests(): Observable<PortalRequest[]> { return this.get('/m/realestate/portal/requests'); }
  raise(body: PortalRequestInput): Observable<PortalRequest> { return this.post('/m/realestate/portal/requests', body); }
  projects(): Observable<PortalProject[]> { return this.get('/m/projects/portal/projects'); }
  acceptances(): Observable<PortalAcceptance[]> { return this.get('/m/projects/portal/acceptances'); }
  sign(id: string, body: PortalSignInput): Observable<PortalAcceptance> { return this.post(`/m/projects/portal/acceptances/${id}/sign`, body); }

  // ---- vendor ----

  vendorBills(): Observable<VendorBill[]> { return this.get('/portal/vendor/bills'); }
  vendorPayments(): Observable<VendorPayment[]> { return this.get('/portal/vendor/payments'); }
  vendorStatement(): Observable<VendorStatement> { return this.get('/portal/vendor/statement'); }
  vendorWorkOrders(): Observable<VendorWorkOrder[]> { return this.get('/m/realestate/portal/vendor/work-orders'); }
  updateWorkOrder(id: string, body: VendorWorkOrderUpdate): Observable<VendorWorkOrder> { return this.post(`/m/realestate/portal/vendor/work-orders/${id}/update`, body); }

  private load(): PortalSession | null {
    try { return JSON.parse(sessionStorage.getItem(KEY) ?? 'null'); } catch { return null; }
  }

  private headers(): HttpHeaders { return new HttpHeaders({ Authorization: `Bearer ${this.session()?.token ?? ''}` }); }
  private get<T>(path: string): Observable<T> { return this.http.get<T>(`${this.base}${path}`, { headers: this.headers() }); }
  private post<T>(path: string, body: unknown): Observable<T> { return this.http.post<T>(`${this.base}${path}`, body, { headers: this.headers() }); }
}
