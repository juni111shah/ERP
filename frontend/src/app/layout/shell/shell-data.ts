import { Signal, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, interval, map, of, startWith, switchMap } from 'rxjs';
import { NavGroup, NavigationApi } from '@core/api/navigation.api';
import { NotificationsApi } from '@core/api/notifications.api';
import { SessionStore } from '@core/session/session.store';

/** The server-driven sidebar menu; it reloads whenever the session changes (workspace switch, module enabled or disabled). Call in an injection context. */
export function sidebarMenu(): Signal<NavGroup[]> {
  const session = inject(SessionStore);
  const navigationApi = inject(NavigationApi);
  return toSignal(toObservable(session.me).pipe(
    switchMap((me) => (me ? navigationApi.menu().pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as NavGroup[] });
}

/**
 * The unread notification count, asked on a session change, after `refresh` changes (the drawer changed something) and every minute.
 * A blocked workspace answers 402 to everything but its billing page, so it is not asked. Call in an injection context.
 */
export function unreadNotifications(refresh: Signal<number>): Signal<{ unread: number }> {
  const session = inject(SessionStore);
  const notificationsApi = inject(NotificationsApi);
  return toSignal(combineLatest([toObservable(session.me), toObservable(refresh), interval(60_000).pipe(startWith(0))]).pipe(
    map(([me]) => me),
    switchMap((me) => (me && me.subscription?.access !== 'blocked' ? notificationsApi.inbox().pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
  ), { initialValue: { unread: 0 } });
}
