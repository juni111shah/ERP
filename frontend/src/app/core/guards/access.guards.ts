import { inject } from '@angular/core';
import { CanMatchFn, Router, UrlTree } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { landsOnConsole } from '../format/subscription';
import { I18nService } from '../i18n/i18n.service';
import { SessionStore } from '../session/session.store';

/**
 * A page the person may not open: home, with a short message. Right after their access changed (the open page is being checked again,
 * see AccessSync) the message says so; otherwise it is a direct link or a bookmark to a page they cannot use.
 */
export function refuseAccess(): UrlTree {
  const session = inject(SessionStore);
  const i18n = inject(I18nService);
  inject(NzMessageService).warning(session.rechecking()
    ? i18n.t('ui.access.pageLost', 'Your access has changed, so that page is no longer available to you.')
    : i18n.t('ui.access.denied', 'You do not have access to that page.'));
  return inject(Router).createUrlTree(['/']);
}

/** Lets the route match when the user has any one of these permissions. */
export function anyPermissionGuard(...permissions: string[]): CanMatchFn {
  return () => { const session = inject(SessionStore); return permissions.some((p) => session.hasPermission(p)) || refuseAccess(); };
}

/** Lets the route match only when the user holds every one of these permissions. */
export function allPermissionsGuard(...permissions: string[]): CanMatchFn {
  return () => { const session = inject(SessionStore); return permissions.every((p) => session.hasPermission(p)) || refuseAccess(); };
}

/** canMatch guard: route only matches when the user holds the permission (<module>.<resource>.<action>). */
export function permissionGuard(permission: string): CanMatchFn {
  return () => inject(SessionStore).hasPermission(permission) || refuseAccess();
}

/**
 * canMatch guard mirroring the API's ModuleEnabledFilter (403 module-disabled): a disabled module's routes redirect
 * home with a toast. Data is kept server-side and returns when the module is re-enabled.
 */
export function moduleEnabledGuard(moduleKey: string): CanMatchFn {
  return () => {
    if (inject(SessionStore).isModuleEnabled(moduleKey)) return true;
    const i18n = inject(I18nService);
    const moduleName = i18n.lang() === 'ar' ? i18n.t('menu.group.' + moduleKey, moduleKey) : moduleKey;
    inject(NzMessageService).warning(
      i18n.t('ui.guard.moduleDisabled', 'The {module} module is not enabled for this workspace.').replace('{module}', moduleName),
    );
    return inject(Router).createUrlTree(['/']);
  };
}

/**
 * For /industry/:module/:type[/:id]: any module key works, so the key is read from the address. The module must be enabled for the
 * workspace, and the person must be allowed to read that record type (`{module}.{type}.read`).
 */
export const industryRecordGuard: CanMatchFn = (route, segments) => {
  const [, module, type] = segments.map((s) => s.path);
  if (!module || !type) return inject(Router).createUrlTree(['/']);
  const enabled = moduleEnabledGuard(module)(route, segments);
  if (enabled !== true) return enabled;
  return permissionGuard(`${module}.${type}.read`)(route, segments);
};

/** Signed-in guard: redirects to the login page and keeps a safe return URL. */
export const authGuard: CanMatchFn = (_route, segments) => {
  if (inject(SessionStore).isAuthenticated()) return true;
  const url = '/' + segments.map((s) => s.path).join('/');
  return inject(Router).createUrlTree(['/auth/login'], { queryParams: { returnUrl: url } });
};

/**
 * The founder's console: signed-out people go to sign in, anyone else who is not on the platform team goes home.
 * The session is normally restored before the first navigation; should /me still be missing, it is fetched here.
 */
export const platformAdminGuard: CanMatchFn = (_route, segments) => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (!session.isAuthenticated()) {
    return router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: '/' + segments.map((s) => s.path).join('/') } });
  }
  const home = router.createUrlTree(['/']);
  const me = session.me();
  if (me) return me.platformAdmin ? true : home;
  return inject(AuthService).loadMe().pipe(map((m) => (m.platformAdmin ? true : home)), catchError(() => of(home)));
};

/** The dashboard route: the platform team, in the platform's own workspace, starts on the console. A deep link never comes through here. */
export const founderLandingGuard: CanMatchFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);
  // The shell reloads a screen by passing through the root without changing the address; that must not leave the page.
  if (router.getCurrentNavigation()?.extras.skipLocationChange) return true;
  return landsOnConsole(session.me(), session.stayInWorkspace()) ? router.createUrlTree(['/platform']) : true;
};

/** The site's home address shows the public front page to signed-out visitors. Signed-in people fall through to their workspace. */
export const signedOutGuard: CanMatchFn = () => !inject(SessionStore).isAuthenticated();

/** A signed-in person who opens the sign-in page, say in another tab, is already in: they go where they were heading. */
export const guestOnlyGuard: CanMatchFn = () => {
  const session = inject(SessionStore);
  const router = inject(Router);
  if (!session.isAuthenticated()) return true;
  const returnUrl = router.getCurrentNavigation()?.extractedUrl.queryParams['returnUrl'];
  return router.parseUrl(safeReturnUrl(typeof returnUrl === 'string' ? returnUrl : null));
};

/** Only relative in-app paths are allowed as return URLs (no open redirects). */
export function safeReturnUrl(raw: string | null | undefined): string {
  if (!raw || !raw.startsWith('/') || raw.startsWith('//') || raw.startsWith('/\\')) return '/';
  return raw;
}
