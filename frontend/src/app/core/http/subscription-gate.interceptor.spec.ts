import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MeResponse, SessionStore } from '../session/session.store';
import { subscriptionGateInterceptor } from './subscription-gate.interceptor';

const READ_ONLY = 'https://erp.local/problems/subscription-read-only';
const BLOCKED = 'https://erp.local/problems/subscription-blocked';

describe('subscription gate interceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let warnings: string[];
  let session: SessionStore;

  const me = (access: 'full' | 'readOnly' | 'blocked'): MeResponse => ({
    user: { id: 'u1', email: 'u@example.test', fullName: 'User', mustChangePassword: false },
    workspace: { id: 'w1', code: 'w1', name: 'Workspace', countryCode: 'AE', baseCurrency: 'AED', timeZone: 'UTC', defaultLocale: 'en' },
    workspaces: [], permissions: [], legalEntities: [], allLegalEntities: true, enabledModules: [],
    subscription: { hasSubscription: true, status: 'active', access, planName: 'P', trialEndsOn: null, trialDaysLeft: null, currentPeriodEnd: null, lapsedOn: null, isComplimentary: false, suspendedReason: null },
  });

  const fail = (url: string, type: string, status = 402) => {
    http.get(url).subscribe({ next: () => undefined, error: () => undefined });
    backend.expectOne(url).flush({ type, detail: 'Changes are paused.' }, { status, statusText: 'Payment required' });
  };

  beforeEach(() => {
    warnings = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([subscriptionGateInterceptor])),
        provideHttpClientTesting(),
        { provide: NzMessageService, useValue: { warning: (t: string) => warnings.push(t) } },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    session.me.set(me('full'));
  });

  afterEach(() => { backend.verify(); session.clear(); });

  it('shows the message once and reloads the session once, however many calls fail', () => {
    fail('/api/v1/a', READ_ONLY);
    fail('/api/v1/b', READ_ONLY);
    fail('/api/v1/c', READ_ONLY);
    expect(warnings).toEqual(['Changes are paused.']);
    backend.expectOne((r) => r.url.endsWith('/me')).flush(me('readOnly'));
    expect(session.me()?.subscription?.access).toBe('readOnly');
  });

  it('does not reload the session when it already knows, so it cannot loop', () => {
    session.me.set(me('blocked'));
    fail('/api/v1/notifications', BLOCKED);
    backend.expectNone((r) => r.url.endsWith('/me'));
    expect(warnings.length).toBe(1);
  });

  it('leaves every other failure alone', () => {
    fail('/api/v1/a', 'https://erp.local/problems/other');
    fail('/api/v1/b', READ_ONLY, 403);
    expect(warnings).toEqual([]);
    backend.expectNone((r) => r.url.endsWith('/me'));
  });
});
