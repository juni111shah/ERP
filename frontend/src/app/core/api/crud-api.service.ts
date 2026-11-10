import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { FilterCriterion, ListQuery, PagedResult } from './api.models';

/**
 * Typed REST client for one resource; components never call HttpClient directly. One line per resource:
 *   @Injectable({ providedIn: 'root' }) export class PartiesApi extends CrudApiService<PartyDto, PartyInput> { protected readonly resource = 'parties'; }
 * Module resources live under 'm/<moduleKey>/...'.
 */
export abstract class CrudApiService<TDto, TInput = Partial<TDto>> {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = inject(API_BASE_URL);
  protected abstract readonly resource: string;

  protected get url(): string {
    return `${this.baseUrl}/${this.resource}`;
  }

  list(query: ListQuery): Observable<PagedResult<TDto>> {
    return this.http.get<PagedResult<TDto>>(this.url, { params: toHttpParams(query) });
  }

  get(id: string): Observable<TDto> {
    return this.http.get<TDto>(`${this.url}/${id}`);
  }

  create(body: TInput): Observable<TDto> {
    return this.http.post<TDto>(this.url, body);
  }

  update(id: string, body: TInput): Observable<TDto> {
    return this.http.put<TDto>(`${this.url}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}

/** A list query as HTTP params: page and size always, then sort, trimmed search, each filter and the extra params. */
export function toHttpParams(q: ListQuery): HttpParams {
  let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
  if (q.sort) params = params.set('sort', q.sort);
  if (q.search?.trim()) params = params.set('search', q.search.trim());
  for (const f of q.filters ?? []) params = params.append('filter', serializeFilter(f));
  for (const [k, v] of Object.entries(q.extra ?? {})) params = params.set(k, v);
  return params;
}

/** One filter as `field:op:value` (`field:op` for the null checks); list values are pipe-joined. */
export function serializeFilter(f: FilterCriterion): string {
  if (f.op === 'isNull' || f.op === 'isNotNull') return `${f.field}:${f.op}`;
  const value = Array.isArray(f.value) ? f.value.join('|') : String(f.value ?? '');
  return `${f.field}:${f.op}:${value}`;
}

/** Only the params that have a value: an empty or missing one is left out of the query string. */
export function definedParams<P extends object>(params: P): Record<string, string> {
  return Object.fromEntries(Object.entries(params).filter(([, v]) => !!v)) as Record<string, string>;
}

/** Saves a record: PUT to `<url>/<id>` when it exists, POST to `<url>` when it is new. */
export function putOrPost<T>(http: HttpClient, url: string, id: string | null, body: unknown): Observable<T> {
  return id ? http.put<T>(`${url}/${id}`, body) : http.post<T>(url, body);
}
