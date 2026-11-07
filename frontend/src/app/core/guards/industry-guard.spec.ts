import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Route, Router, UrlSegment, UrlTree, provideRouter } from '@angular/router';
import { MeResponse, SessionStore } from '../session/session.store';
import { industryRecordGuard } from './access.guards';

const me = (permissions: string[], modules: string[]): MeResponse => ({
  user: { id: 'u1', email: 'u@example.test', fullName: 'User', mustChangePassword: false },
  workspace: { id: 'w1', code: 'w1', name: 'Workspace', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
  workspaces: [], permissions, legalEntities: [], allLegalEntities: true, enabledModules: modules.map((key) => ({ key, uiRoute: '/' + key })),
});

describe('industryRecordGuard', () => {
  let session: SessionStore;
  let router: Router;
  const at = (module: string, type: string) => [new UrlSegment('industry', {}), new UrlSegment(module, {}), new UrlSegment(type, {})];
  const run = (module: string, type: string) => runInInjectionContext(TestBed.inject(EnvironmentInjector), () => industryRecordGuard({} as Route, at(module, type)));

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations(), provideRouter([])] });
    session = TestBed.inject(SessionStore);
    router = TestBed.inject(Router);
    session.clear();
  });
  afterEach(() => session.clear());

  it('lets a person in when the module is on and they can read the record type, whatever the module key is', () => {
    session.me.set(me(['clinic.visits.read'], ['clinic']));
    expect(run('clinic', 'visits')).toBeTrue();
  });

  it('sends them home when the module is not enabled', () => {
    session.me.set(me(['clinic.visits.read'], []));
    expect(router.serializeUrl(run('clinic', 'visits') as UrlTree)).toBe('/');
  });

  it('sends them home when they cannot read that record type', () => {
    session.me.set(me(['clinic.patients.read'], ['clinic']));
    expect(router.serializeUrl(run('clinic', 'visits') as UrlTree)).toBe('/');
  });
});
