import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** What to send: an empty recipient means the party's own address. */
export interface DocumentEmailInput { to: string | null; message: string | null; }

/** Sends a document or statement by email through its own endpoint, for example "finance/invoices/{id}/email". */
@Injectable({ providedIn: 'root' })
export class DocumentEmailApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** `path` is relative to the API base; `params` become the query string. Answers with the address it was sent to. */
  send(path: string, input: DocumentEmailInput, params: Record<string, string>): Observable<{ to: string }> {
    return this.http.post<{ to: string }>(`${this.base}/${path}`, input, { params });
  }
}
