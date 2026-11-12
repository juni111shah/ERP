import { WritableSignal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';

/**
 * Loads `source` into `target` (optionally mapped); when it fails, `target` gets `fallback` (an empty list, null...).
 * Replaces `source.pipe(catchError(() => of(empty))).subscribe((v) => sig.set(map(v)))`. To leave the signal as it was instead, use `loadInto`.
 */
export function loadOrFallback<T, R = T>(target: WritableSignal<R>, source: Observable<T>, fallback: R, map?: (value: T) => R): Subscription {
  return source.subscribe({
    next: (value) => target.set(map ? map(value) : (value as unknown as R)),
    error: () => target.set(fallback),
  });
}
