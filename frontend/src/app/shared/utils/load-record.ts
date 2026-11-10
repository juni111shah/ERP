import { WritableSignal } from '@angular/core';
import { Observable } from 'rxjs';

/** The state a detail page shows while its record loads: a spinner, then the record or an error panel. */
export interface RecordState<T> {
  loading: WritableSignal<boolean>;
  error: WritableSignal<string | null>;
  record: WritableSignal<T | null>;
}

/** What the error panel says: no id in the address, or the server's message (else `failed`). */
export interface RecordLoadText { invalid: string; failed: string; }

/**
 * Loads the record of a detail page (`/<area>/<things>/:id`): sets it and stops the spinner, then runs `loaded` (sections that
 * need the record). A missing id or a failed request ends in the error panel with the server's message when it sent one.
 */
export function loadRecord<T>(id: string | null, fetch: (id: string) => Observable<T>, state: RecordState<T>, text: RecordLoadText, loaded?: (record: T) => void): void {
  if (!id) {
    state.error.set(text.invalid);
    state.loading.set(false);
    return;
  }
  fetch(id).subscribe({
    next: (record) => {
      state.record.set(record);
      state.loading.set(false);
      loaded?.(record);
    },
    error: (err: { error?: { message?: string } | null }) => {
      state.error.set(err.error?.message || text.failed);
      state.loading.set(false);
    },
  });
}
