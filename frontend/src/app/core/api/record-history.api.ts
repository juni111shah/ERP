import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { PagedResult } from './api.models';

export interface FieldChange { field: string; old?: string | null; new?: string | null; }
/** One change of a record in the audit trail: who, when, and each field from old to new. */
export interface HistoryEntry { id: string; occurredAt: string; userId?: string; userName?: string; action: string; changes: FieldChange[]; }

/** The change history of one record, from the audit trail (/api/v1/audit/record), newest first. */
@Injectable({ providedIn: 'root' })
export class RecordHistoryApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  list(recordType: string, recordId: string, page: number, pageSize: number): Observable<PagedResult<HistoryEntry>> {
    return this.http.get<PagedResult<HistoryEntry>>(`${this.base}/audit/record`, { params: { recordType, recordId, page, pageSize } });
  }
}
