import { HttpParams } from '@angular/common/http';
import { FilterCriterion, ListQuery } from './api.models';

/** Serializes a criterion as `field:op:value` (array values are pipe-joined, valueless operators omit the value). */
export function serializeFilter(f: FilterCriterion): string {
  const value = Array.isArray(f.value) ? f.value.join('|') : (f.value ?? '');
  return f.op === 'isNull' || f.op === 'isNotNull' ? `${f.field}:${f.op}` : `${f.field}:${f.op}:${value}`;
}

/** Shared list-query -> HttpParams mapping (`page`, `pageSize`, `sort`, `q`, repeated `filter`). Use for every paged endpoint. */
export function toListParams(query: ListQuery, extra: Record<string, string | number | boolean | null | undefined> = {}): HttpParams {
  let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
  if (query.sort) params = params.set('sort', query.sort);
  if (query.q) params = params.set('q', query.q);
  for (const f of query.filters ?? []) params = params.append('filter', serializeFilter(f));
  for (const [k, v] of Object.entries(extra)) if (v != null && v !== '') params = params.set(k, String(v));
  return params;
}
