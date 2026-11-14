import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { permissionGuard, platformAdminGuard } from '../guards/access.guards';
import { MeResponse, SessionStore } from '../session/session.store';
import { holdsPermission } from '@shared/directives/has-permission.directive';
import { AccessSync } from './access-sync.service';
import { hubUrl } from './realtime.service';

const me = (permissions: string[]): MeResponse => ({
  user: { id: 'u1', email: 'u@example.test', fullName: 'User', mustChangePassword: false },
  workspace: { id: 'w1', code: 'w1', name: 'Workspace', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
  workspaces: [], permissions, legalEntities: [], allLegalEntities: true, enabledModules: [],
});

describe('live access changes', () => {
  it('finds the hub next to the API root', () => {
    expect(hubUrl('/api/v1')).toBe('/hubs/notifications');
    expect(hubUrl('https://erp.example.test/api/v1')).toBe('https://erp.example.test/hubs/notifications');
  });

  it('checks one permission, any of several, or all of them', () => {
    const session = { hasPermission: (p: string) => p === 'a.b.read' };
    expect(holdsPermission(session, 'a.b.read')).toBeTrue();
    expect(holdsPermission(session, ['x.y.write', 'a.b.read'])).toBeTrue();
    expect(holdsPermission(session, ['x.y.write', 'a.b.read'], 'all')).toBeFalse();
    expect(holdsPermission(session, null)).toBeTrue();
    expect(holdsPermission(session, [])).toBeTrue();
  });

  describe('AccessSync', () => {
    let refreshes: number;
    let session: SessionStore;
    let router: Router;
    let warnings: string[];

    beforeEach(() => {
      refreshes = 0;
      warnings = [];
      TestBed.configureTestingModule({
        providers: [
          provideNoopAnimations(),
          provideRouter([]),
          { provide: AuthService, useValue: { refresh: () => { refreshes++; return of('new-token'); } } },
          { provide: NzMessageService, useValue: { warning: (m: string) => warnings.push(m) } },
        ],
      });
      session = TestBed.inject(SessionStore);
      router = TestBed.inject(Router);
      session.accessToken.set('old-token');
      session.me.set(me(['core.parties.read']));
    });
    afterEach(() => session.clear());

    it('makes one session refresh out of a burst of changes and checks the open page again', fakeAsync(() => {
      const sync = TestBed.inject(AccessSync);
      const nav = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
      sync.refresh('role-changed');
      sync.refresh('modules-changed');
      sync.refresh('roles-assigned');
      tick(400);
      expect(refreshes).toBe(1);
      expect(nav).toHaveBeenCalledWith(router.url, jasmine.objectContaining({ onSameUrlNavigation: 'reload' }));
    }));

    it('lets a guard say the access changed while the open page is checked again', () => {
      const guard = () => runInInjectionContext(TestBed.inject(EnvironmentInjector), () => permissionGuard('finance.invoices.read')({}, []));
      expect(router.serializeUrl(guard() as UrlTree)).toBe('/');
      session.rechecking.set(true);
      guard();
      expect(warnings).toEqual(['You do not have access to that page.', 'Your access has changed, so that page is no longer available to you.']);
    });

    it('reads the platform team role: support, manager or owner', () => {
      session.me.set({ ...me([]), platformAdmin: true, platformRole: 'support' });
      expect(session.platformManager()).toBeFalse();
      expect(session.platformOwner()).toBeFalse();
      session.me.set({ ...me([]), platformAdmin: true, platformRole: 'manager' });
      expect(session.platformManager()).toBeTrue();
      expect(session.platformOwner()).toBeFalse();
      session.me.set({ ...me([]), platformAdmin: true, platformRole: 'owner' });
      expect(session.platformManager()).toBeTrue();
      expect(session.platformOwner()).toBeTrue();
      session.me.set(me([]));
      expect(session.platformManager()).toBeFalse();
    });

    it('says why the console closed when the person was taken off the platform team', () => {
      const guard = () => runInInjectionContext(TestBed.inject(EnvironmentInjector), () => platformAdminGuard({}, []));
      session.me.set({ ...me([]), platformAdmin: true, platformRole: 'support' });
      expect(guard()).toBeTrue();
      session.me.set({ ...me([]), platformAdmin: false, platformRole: null });
      expect(router.serializeUrl(guard() as UrlTree)).toBe('/');
      expect(warnings).toEqual([]);
      session.rechecking.set(true);
      expect(router.serializeUrl(guard() as UrlTree)).toBe('/');
      expect(warnings).toEqual(['Your access has changed, so that page is no longer available to you.']);
    });
  });
});
