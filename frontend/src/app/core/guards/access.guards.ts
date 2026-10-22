import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { SessionStore } from '../session/session.store';

/** canMatch guard: route only matches when the user holds the permission (<module>.<resource>.<action>). */
export function permissionGuard(permission: string): CanMatchFn {
  return () => inject(SessionStore).hasPermission(permission) || inject(Router).createUrlTree(['/']);
}

/**
 * canMatch guard mirroring the API's ModuleEnabledFilter (403 module-disabled): a disabled module's routes redirect
 * home with a toast. Data is kept server-side and returns when the module is re-enabled.
 */
export function moduleEnabledGuard(moduleKey: string): CanMatchFn {
  return () => {
    if (inject(SessionStore).isModuleEnabled(moduleKey)) return true;
    inject(NzMessageService).warning(`The ${moduleKey} module is not enabled for this workspace.`);
    return inject(Router).createUrlTree(['/']);
  };
}

/** Signed-in guard: redirects to the login page and keeps a safe return URL. */
export const authGuard: CanMatchFn = (_route, segments) => {
  if (inject(SessionStore).isAuthenticated()) return true;
  const url = '/' + segments.map((s) => s.path).join('/');
  return inject(Router).createUrlTree(['/auth/login'], { queryParams: { returnUrl: url } });
};

/** Only relative in-app paths are allowed as return URLs (no open redirects). */
export function safeReturnUrl(raw: string | null | undefined): string {
  if (!raw || !raw.startsWith('/') || raw.startsWith('//') || raw.startsWith('/\\')) return '/';
  return raw;
}
