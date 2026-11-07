import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { LookupOption } from '../custom-fields/lookup-registry';
import { API_BASE_URL } from './api-config';
import { ListQuery, PagedResult } from './api.models';
import { toHttpParams } from './crud-api.service';

/** A field a record type declares for its own details (kept under `data` on the record). */
export interface RecordFieldDef {
  key: string; label: string; type: 'text' | 'textarea' | 'number' | 'money' | 'date' | 'bool' | 'select' | string;
  required: boolean; options?: string[] | null; sensitive: boolean; inList: boolean; help?: string | null;
}
export interface RecordStatusDef { key: string; label: string; tone: string; }
export interface RecordTransitionDef { from: string; to: string; label: string; reasonRequired: boolean; approval: boolean; }

/** One kind of record a module offers, as described by GET /m/{module}/definitions. */
export interface RecordTypeView {
  key: string; label: string; singular: string; icon: string; fields: RecordFieldDef[]; statuses: RecordStatusDef[];
  partyRole?: string | null; partyLabel?: string | null; parentType?: string | null; parentLabel?: string | null;
  hasLines: boolean; linesLabel?: string | null; hasAmount: boolean; hasDueDate: boolean; canInvoice: boolean; canBill: boolean; perLineBilling: boolean;
  billableStatuses?: string[] | null; consumesStock: boolean; tracksProgress: boolean; sensitive: boolean; titleLabel?: string | null;
}
export interface PackView { key: string; name: string; description: string; types: RecordTypeView[]; }

export interface RecordLineDto {
  id: string; seq: number; code?: string | null; description: string; itemId?: string | null; unitOfMeasure?: string | null;
  quantity: number; unitPrice: number; amount: number; completedQuantity: number; dueDate?: string | null; invoiceId?: string | null;
}
export interface RecordEventDto { id: string; fromStatus?: string | null; toStatus: string; occurredAt: string; userName?: string | null; reason?: string | null; }
export interface IndustryRecordDto {
  id: string; moduleKey: string; recordType: string; legalEntityId: string; number: string; title: string; status: string;
  partyId?: string | null; partyName?: string | null; parentId?: string | null; parentTitle?: string | null; businessUnitId?: string | null;
  recordDate: string; dueDate?: string | null; amount: number; data: Record<string, unknown>; notes?: string | null;
  invoiceId?: string | null; billId?: string | null; consumedOn?: string | null; createdAt: string;
  lines: RecordLineDto[]; events: RecordEventDto[]; nextSteps: RecordTransitionDef[];
}

export interface RecordLineInput {
  description: string; quantity: number; unitPrice: number; code?: string | null; itemId?: string | null; unitOfMeasure?: string | null;
  dueDate?: string | null; completedQuantity?: number | null;
}
export interface RecordInput {
  legalEntityId: string; title: string; recordDate: string; data?: Record<string, unknown>; partyId?: string | null; parentId?: string | null;
  businessUnitId?: string | null; dueDate?: string | null; amount?: number | null; notes?: string | null; lines?: RecordLineInput[];
}
export interface RecordBillingBody { date?: string | null; taxCodeId?: string | null; lineIds?: string[] | null; vendorReference?: string | null; }
export interface RecordConsumeBody { date?: string | null; warehouseId?: string | null; }

/** The record types of every module share these calls; only the module key and type key change. */
@Injectable({ providedIn: 'root' })
export class IndustryApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly packs = new Map<string, Observable<PackView>>();

  private records(module: string, type: string): string { return `${this.base}/m/${encodeURIComponent(module)}/records/${encodeURIComponent(type)}`; }

  /** The record types the person may read; remembered per module until a load fails. */
  definitions(module: string): Observable<PackView> {
    let pack = this.packs.get(module);
    if (!pack) {
      pack = this.http.get<PackView>(`${this.base}/m/${encodeURIComponent(module)}/definitions`).pipe(
        catchError((e) => { this.packs.delete(module); throw e; }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
      this.packs.set(module, pack);
    }
    return pack;
  }
  forgetDefinitions(): void { this.packs.clear(); }

  list(module: string, type: string, query: ListQuery): Observable<PagedResult<IndustryRecordDto>> {
    return this.http.get<PagedResult<IndustryRecordDto>>(this.records(module, type), { params: toHttpParams(query) });
  }
  get(module: string, type: string, id: string): Observable<IndustryRecordDto> { return this.http.get<IndustryRecordDto>(`${this.records(module, type)}/${id}`); }
  create(module: string, type: string, body: RecordInput): Observable<IndustryRecordDto> { return this.http.post<IndustryRecordDto>(this.records(module, type), body); }
  update(module: string, type: string, id: string, body: RecordInput): Observable<IndustryRecordDto> { return this.http.put<IndustryRecordDto>(`${this.records(module, type)}/${id}`, body); }
  remove(module: string, type: string, id: string): Observable<void> { return this.http.delete<void>(`${this.records(module, type)}/${id}`); }
  transition(module: string, type: string, id: string, to: string, reason?: string | null): Observable<IndustryRecordDto> {
    return this.http.post<IndustryRecordDto>(`${this.records(module, type)}/${id}/transition`, { to, reason: reason || null });
  }
  invoice(module: string, type: string, id: string, body: RecordBillingBody): Observable<IndustryRecordDto> { return this.http.post<IndustryRecordDto>(`${this.records(module, type)}/${id}/invoice`, body); }
  bill(module: string, type: string, id: string, body: RecordBillingBody): Observable<IndustryRecordDto> { return this.http.post<IndustryRecordDto>(`${this.records(module, type)}/${id}/bill`, body); }
  consume(module: string, type: string, id: string, body: RecordConsumeBody): Observable<IndustryRecordDto> { return this.http.post<IndustryRecordDto>(`${this.records(module, type)}/${id}/consume`, body); }
  progress(module: string, type: string, id: string, lines: { lineId: string; completedQuantity: number }[]): Observable<IndustryRecordDto> {
    return this.http.post<IndustryRecordDto>(`${this.records(module, type)}/${id}/progress`, { lines });
  }

  /** Parties for a picker. `role` is the type's party role: "Any" (or none) means every party, otherwise only that role. */
  partyLookup(role: string | null | undefined, term: string): Observable<LookupOption[]> {
    const filters = role && role.toLowerCase() !== 'any' ? [{ field: 'roles', op: 'has' as const, value: role.toLowerCase() }] : [];
    return this.http.get<PagedResult<{ id: string; code?: string; name: string }>>(`${this.base}/parties`, { params: toHttpParams({ page: 1, pageSize: 20, search: term, filters }) })
      .pipe(map((r) => r.items.map((p) => ({ value: p.id, label: p.code ? `${p.code} · ${p.name}` : p.name }))), catchError(() => of([])));
  }
  /** Records of another type in the same module, for a parent picker. */
  recordLookup(module: string, type: string, term: string): Observable<LookupOption[]> {
    return this.list(module, type, { page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((x) => ({ value: x.id, label: `${x.number} · ${x.title}` }))), catchError(() => of([])));
  }
}

/** Colour of a status tag from the tone the definition gives it. */
export function toneColor(tone: string | null | undefined): string {
  switch (tone) { case 'success': case 'processing': case 'warning': case 'error': return tone; default: return 'default'; }
}
export function statusLabel(type: RecordTypeView | null | undefined, status: string): string {
  return type?.statuses.find((s) => s.key === status)?.label ?? status;
}
