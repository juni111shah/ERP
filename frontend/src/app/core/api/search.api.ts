import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

/** One record found by the global search, with the app link that opens it. */
export interface SearchHit { category: string; title: string; subtitle?: string; link: string; }

/** One query across the core and the enabled modules (/api/v1/search). */
@Injectable({ providedIn: 'root' })
export class GlobalSearchApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  search(q: string): Observable<{ hits: SearchHit[] }> { return this.http.get<{ hits: SearchHit[] }>(`${this.base}/search`, { params: { q } }); }
}
