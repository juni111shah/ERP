import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../api/api-config';
import { SessionStore } from '../session/session.store';

/** Adds X-Entity-Id (spec 4.30 / 12) to API calls when a single legal entity is selected; "All" sends nothing. */
export const entityScopeInterceptor: HttpInterceptorFn = (req, next) => {
  const base = inject(API_BASE_URL);
  const entityId = inject(SessionStore).currentEntityId();
  if (!entityId || !req.url.startsWith(base) || req.headers.has('X-Entity-Id')) return next(req);
  return next(req.clone({ setHeaders: { 'X-Entity-Id': entityId } }));
};
