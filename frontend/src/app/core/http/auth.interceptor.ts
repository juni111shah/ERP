import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthService } from '../auth/auth.service';
import { SessionStore } from '../session/session.store';

const AUTH_PATHS = ['/auth/login', '/auth/refresh', '/auth/select-workspace', '/auth/logout'];

/** Adds the bearer token, the CSRF client header and credentials; on 401 refreshes once and retries. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const base = inject(API_BASE_URL);
  // Tenant-portal calls carry their own token (see PortalService) and must never trigger the staff refresh/redirect flow.
  if (!req.url.startsWith(base) || req.url.includes('/portal/') || req.url.includes('/m/realestate/portal')) return next(req);
  const session = inject(SessionStore);
  const auth = inject(AuthService);
  const router = inject(Router);

  const withAuth = (token: string | null) => req.clone({
    withCredentials: true,
    setHeaders: { 'X-ERP-Client': 'web', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  });

  const toLogin = () => void router.navigateByUrl(`/auth/login?returnUrl=${encodeURIComponent(router.url)}`);
  const sentToken = session.accessToken();

  return next(withAuth(sentToken)).pipe(
    catchError((err: unknown) => {
      const isAuthCall = AUTH_PATHS.some((p) => req.url.includes(p));
      if (!(err instanceof HttpErrorResponse) || err.status !== 401 || isAuthCall) return throwError(() => err);
      // A refresh already rotated the token while this request was in flight: retry with the current token rather than rotating again.
      const current = session.accessToken();
      const fresh$ = current && current !== sentToken ? of(current) : auth.refresh();
      return fresh$.pipe(switchMap((token) => {
        if (!token) {
          toLogin();
          return throwError(() => err);
        }
        return next(withAuth(token)).pipe(catchError((retryErr: unknown) => {
          // Still 401 with a freshly issued token: the session is not usable, so sign out instead of sitting on the page.
          if (retryErr instanceof HttpErrorResponse && retryErr.status === 401) {
            session.clear();
            toLogin();
          }
          return throwError(() => retryErr);
        }));
      }));
    }),
  );
};
