import { Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, of } from 'rxjs';

/**
 * A side list (a page of entities, accounts, employees...) loaded once into a signal as `{ items }`: empty until it arrives and
 * when the call fails. Call it in an injection context (a field initializer). Replaces the
 * `toSignal(api.list(q).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } })` copies.
 */
export function itemsSignal<T>(source: Observable<{ items: T[] }>): Signal<{ items: T[] }> {
  return toSignal(source.pipe(catchError(() => of({ items: [] as T[] }))), { initialValue: { items: [] as T[] } });
}
