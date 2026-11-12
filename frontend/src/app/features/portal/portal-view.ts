/** View helpers shared by the portal pages: money text and how a failed list load is handled. */
import { OperatorFunction, catchError, of } from 'rxjs';

/** "QAR 1,500.00", "-QAR 1,500.00", or just the number when no currency is known. */
export function portalMoney(value: number | null | undefined, code: string): string {
  const n = value ?? 0;
  const text = Math.abs(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return `${n < 0 ? '-' : ''}${code ? code + ' ' : ''}${text}`;
}

/** What a portal page does when one of its lists fails to load. */
export interface PortalLoadHandlers {
  /** the portal session has expired (401) */
  signOut: () => void;
  /** a real failure: anything but 401, 403 and 404 */
  failed: () => void;
}

/**
 * Replaces a failed portal list with `fallback`. A disabled module answers 403 and a missing record 404: that is not a failure,
 * the party simply has none of those records. A 401 signs the party out.
 */
export function portalFallback<T>(fallback: T, handlers: PortalLoadHandlers): OperatorFunction<T, T> {
  return catchError((e: { status?: number }) => {
    if (e.status === 401) handlers.signOut();
    else if (e.status !== 403 && e.status !== 404) handlers.failed();
    return of(fallback);
  });
}
