import { Observable, Subscription } from 'rxjs';

/**
 * Shows the result of a small, safe action at once and quietly puts it back if the server says no. Use it for things like a
 * read mark or a done tick, never for money, posting or anything that cannot be undone.
 */
export function optimistic<T>(request: Observable<T>, steps: { apply: () => void; rollback: () => void; done?: (result: T) => void; failed?: (error: unknown) => void }): Subscription {
  steps.apply();
  return request.subscribe({
    next: (result) => steps.done?.(result),
    error: (error: unknown) => { steps.rollback(); steps.failed?.(error); },
  });
}
