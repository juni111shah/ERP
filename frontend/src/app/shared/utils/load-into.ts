import { WritableSignal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

/**
 * Loads `source` into `target` (optionally mapped) and ignores a failure, leaving the signal as it was.
 * For side lists a page can live without (pickers, KPI cards); replaces `subscribe({ next: (v) => sig.set(v), error: () => {} })`.
 */
export function loadInto<T, R = T>(target: WritableSignal<R>, source: Observable<T>, map?: (value: T) => R): Subscription {
  return source.subscribe({
    next: (value) => target.set(map ? map(value) : (value as unknown as R)),
    error: () => {},
  });
}
