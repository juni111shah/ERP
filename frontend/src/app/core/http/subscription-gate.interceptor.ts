import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, finalize, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { SessionStore, SubscriptionSummary } from '../session/session.store';
import { errorBody } from './problem-details';

const QUIET_MS = 10_000;

/** The API's type for each state, against what the session should then show. */
function accessOf(type: unknown): SubscriptionSummary['access'] | null {
  if (typeof type !== 'string') return null;
  if (type.endsWith('subscription-read-only')) return 'readOnly';
  if (type.endsWith('subscription-blocked')) return 'blocked';
  return null;
}

/** A page can fail many calls at once: this keeps that to one message and one reload of the session. */
@Injectable({ providedIn: 'root' })
export class SubscriptionGateNotice {
  private readonly session = inject(SessionStore);
  private readonly auth = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private lastShownAt = 0;
  private lastText = '';
  private reloading = false;

  /** Shows the server's reason (once per quiet period) and reloads the session when it does not yet know the new access. */
  handle(access: SubscriptionSummary['access'], text: string): void {
    const now = Date.now();
    if (text !== this.lastText || now - this.lastShownAt > QUIET_MS) {
      this.lastText = text;
      this.lastShownAt = now;
      this.message.warning(text);
    }
    // The session already says so: reloading it would only start the same calls again.
    if (this.reloading || this.session.me()?.subscription?.access === access) return;
    this.reloading = true;
    this.auth.loadMe().pipe(finalize(() => (this.reloading = false))).subscribe({ error: () => undefined });
  }
}

/** A workspace held back by its subscription: the API answers 402. Shows why, and brings up the banner or the blocked page. */
export const subscriptionGateInterceptor: HttpInterceptorFn = (req, next) => {
  const notice = inject(SubscriptionGateNotice);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 402) {
        const body = errorBody(err);
        const access = accessOf(body?.type);
        if (access) notice.handle(access, body?.detail ?? body?.message ?? 'This workspace is held back by its subscription.');
      }
      return throwError(() => err);
    }),
  );
};
