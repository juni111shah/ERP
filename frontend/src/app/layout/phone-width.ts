import { DestroyRef, inject } from '@angular/core';

/** Phone width: the sidebar is a drawer over the page rather than a column beside it. */
export const PHONE_MEDIA = '(max-width: 767px)';
/** Tablet width: the workspace sidebar starts icon-only, to leave room for tables. */
export const TABLET_MEDIA = '(max-width: 1100px)';

/**
 * Calls `apply` now and each time the window crosses the phone width, until the caller is destroyed (call it in an injection context).
 * Returns false, watching nothing, where the browser cannot tell (no window or no matchMedia).
 */
export function watchPhoneWidth(apply: (phone: boolean) => void): boolean {
  if (typeof window === 'undefined' || !window.matchMedia) return false;
  const phone = window.matchMedia(PHONE_MEDIA);
  const listener = () => apply(phone.matches);
  listener();
  phone.addEventListener('change', listener);
  inject(DestroyRef).onDestroy(() => phone.removeEventListener('change', listener));
  return true;
}
