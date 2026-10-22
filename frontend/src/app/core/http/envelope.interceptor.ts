import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { ApiResponse, PagedResult } from '../api/api.models';

function isEnvelope(body: unknown): body is ApiResponse<unknown> {
  return !!body && typeof body === 'object' && 'success' in body && 'statusCode' in body;
}

/**
 * Unwraps the standard API envelope so services and components work with plain payloads:
 * `{ success, data }` → `data`; paged `{ data, meta }` → `{ items: data, ...meta }`.
 * Error envelopes are left on HttpErrorResponse.error (they keep the RFC 7807 members; see problem-details.ts).
 */
export const envelopeInterceptor: HttpInterceptorFn = (req, next) => {
  const base = inject(API_BASE_URL);
  if (!req.url.startsWith(base)) return next(req);
  return next(req).pipe(map((event) => {
    if (!(event instanceof HttpResponse) || !isEnvelope(event.body) || !event.body.success) return event;
    const body = event.body;
    const payload = body.meta ? ({ items: body.data as unknown[], ...body.meta } satisfies PagedResult<unknown>) : body.data;
    return event.clone({ body: payload });
  }));
};
