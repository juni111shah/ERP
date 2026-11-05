import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Route, Router, UrlSegment, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom, isObservable } from 'rxjs';
import { MeResponse, SessionStore } from '../session/session.store';
import { founderLandingGuard, platformAdminGuard } from './access.guards';

const me = (over: Partial<MeResponse>): MeResponse => ({
  user: { id: 'u1', email: 'u@example.test', fullName: 'User', mustChangePassword: false },
  workspace: { id: 'w1', code: 'w1', name: 'Workspace', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
  workspaces: [], permissions: [], legalEntities: [], allLegalEntities: true, enabledModules: [],
  ...over,
});

type GuardResult = boolean | UrlTree | Observable<boolean | UrlTree>;

describe('platform guards', () => {
  let session: SessionStore;
  let router: Router;
  const segments = [new UrlSegment('platform', {}), new UrlSegment('clients', {})];

  const runAdmin = () => runInInjectionContext(TestBed.inject(EnvironmentInjector), () => platformAdminGuard({} as Route, segments)) as GuardResult;
  const runLanding = () => runInInjectionContext(TestBed.inject(EnvironmentInjector), () => founderLandingGuard({} as Route, [])) as GuardResult;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    session = TestBed.inject(SessionStore);
    router = TestBed.inject(Router);
    session.clear();
  });

  afterEach(() => session.clear());

  it('sends signed-out people to sign in and keeps where they were going', () => {
    const result = runAdmin() as UrlTree;
    expect(router.serializeUrl(result)).toBe('/auth/login?returnUrl=%2Fplatform%2Fclients');
  });

  it('lets the platform team in', () => {
    session.accessToken.set('token');
    session.me.set(me({ platformAdmin: true }));
    expect(runAdmin()).toBeTrue();
  });

  it('sends everyone else home', () => {
    session.accessToken.set('token');
    session.me.set(me({ platformAdmin: false }));
    expect(router.serializeUrl(runAdmin() as UrlTree)).toBe('/');
    session.me.set(me({}));
    expect(router.serializeUrl(runAdmin() as UrlTree)).toBe('/');
  });

  it('loads the session first when it is still missing', async () => {
    session.accessToken.set('token');
    const result = runAdmin();
    expect(isObservable(result)).toBeTrue();
    const pending = firstValueFrom(result as Observable<boolean | UrlTree>);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne((r) => r.url.endsWith('/me')).flush(me({ platformAdmin: true }));
    expect(await pending).toBeTrue();
    http.verify();
  });

  it('goes home when the session cannot be loaded', async () => {
    session.accessToken.set('token');
    const pending = firstValueFrom(runAdmin() as Observable<boolean | UrlTree>);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne((r) => r.url.endsWith('/me')).flush('nope', { status: 500, statusText: 'Server error' });
    expect(router.serializeUrl((await pending) as UrlTree)).toBe('/');
  });

  it('lands the founder on the console from the root route', () => {
    session.me.set(me({ platformAdmin: true, isInternalWorkspace: true }));
    expect(router.serializeUrl(runLanding() as UrlTree)).toBe('/platform');
  });

  it('keeps the founder in the workspace once they chose to go back', () => {
    session.me.set(me({ platformAdmin: true, isInternalWorkspace: true }));
    session.setStayInWorkspace(true);
    expect(runLanding()).toBeTrue();
  });

  it('leaves normal users on their home page', () => {
    session.me.set(me({}));
    expect(runLanding()).toBeTrue();
    session.me.set(me({ platformAdmin: true, isInternalWorkspace: false }));
    expect(runLanding()).toBeTrue();
  });
});
