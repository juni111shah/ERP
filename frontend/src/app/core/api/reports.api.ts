import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** The server-built reports: each report names its own endpoint (see the report registry), so the paths come from the caller. */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** Runs the report at `endpoint` (a path under the API root) with its filter values. */
  run(endpoint: string, params: Record<string, string>): Observable<unknown> { return this.http.get<unknown>(`${this.base}/${endpoint}`, { params }); }

  /** A report's extra action (for example posting a provision) for one legal entity as of a date. */
  action(path: string, legalEntityId: string, asOf: string): Observable<unknown> { return this.http.post<unknown>(`${this.base}/${path}`, { legalEntityId, asOf }); }

  /** Parties matching a search, for a report's party filter (the property owner). */
  searchParties(term: string): Observable<{ items: { id: string; name: string }[] }> {
    return this.http.get<{ items: { id: string; name: string }[] }>(`${this.base}/parties`, { params: { search: term, pageSize: 20 } });
  }
}
