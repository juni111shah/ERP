import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService, LoginResponse, TwoFactorChallengeSetup } from '../core/auth/auth.service';
import { I18nService } from '../core/i18n/i18n.service';
import { LoginPage } from './login.page';
import { TwoFactorStepComponent } from './two-factor-step.component';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { CheckCircleFill, SafetyCertificateFill, SafetyCertificateOutline, ThunderboltFill } from '@ant-design/icons-angular/icons';
import { APP_ICONS } from '../icons';

/** The app icons plus the sign-in page's own decorative ones, so nothing is fetched over HTTP in tests. */
const ICONS = [...APP_ICONS, SafetyCertificateOutline, SafetyCertificateFill, CheckCircleFill, ThunderboltFill];

const PNG = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';
const SETUP: TwoFactorChallengeSetup = {
  otpAuthUri: 'otpauth://totp/ERP:ana%40acme.test?secret=JBSWY3DPEHPK3PXP&issuer=ERP&digits=6&period=30',
  secret: 'JBSW Y3DP EHPK 3PXP', qrCodeDataUri: PNG, issuer: 'ERP', account: 'ana@acme.test', digits: 6, period: 30,
};
const SESSION: LoginResponse = { accessToken: 'at', workspaces: [] } as unknown as LoginResponse;
const problem = (status: number, detail: string) => new HttpErrorResponse({ status, error: { status, detail } });

/** A fake AuthService: records calls and answers from per-test queues. */
class FakeAuth {
  setupCalls: string[] = [];
  verifyCalls: { token: string; code: string }[] = [];
  loginAnswer: Observable<LoginResponse> = of(SESSION);
  setupAnswer: Observable<TwoFactorChallengeSetup> = of(SETUP);
  verifyAnswers: Observable<LoginResponse>[] = [];
  login(): Observable<LoginResponse> { return this.loginAnswer; }
  beginTwoFactorSetup(token: string): Observable<TwoFactorChallengeSetup> { this.setupCalls.push(token); return this.setupAnswer; }
  verifyTwoFactor(token: string, code: string): Observable<LoginResponse> {
    this.verifyCalls.push({ token, code });
    return this.verifyAnswers.shift() ?? of(SESSION);
  }
  selectWorkspace(): Observable<void> { return of(undefined); }
}

describe('Two-factor sign-in step', () => {
  let auth: FakeAuth;
  let fixture: ComponentFixture<TwoFactorStepComponent>;
  const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const type = (value: string) => {
    const input = el('twofa-code') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(async () => {
    try { localStorage.removeItem('erp.ui.prefs'); } catch { /* ignore */ }
    auth = new FakeAuth();
    await TestBed.configureTestingModule({
      imports: [TwoFactorStepComponent],
      providers: [provideNoopAnimations(), provideRouter([]), provideNzIcons(ICONS), { provide: AuthService, useValue: auth }],
    }).compileComponents();
  });
  afterEach(() => { try { localStorage.removeItem('erp.ui.prefs'); } catch { /* ignore */ } });

  const create = (mode: 'setup' | 'verify') => {
    fixture = TestBed.createComponent(TwoFactorStepComponent);
    fixture.componentRef.setInput('mode', mode);
    fixture.componentRef.setInput('challengeToken', 'chal-1');
    fixture.detectChanges(); tick(); fixture.detectChanges();
    return fixture.componentInstance;
  };

  it('setup: loads the QR for the challenge, hides the key until "Can\'t scan? Show key", and copies it', fakeAsync(() => {
    const writes: string[] = [];
    spyOn(navigator.clipboard, 'writeText').and.callFake((t: string) => { writes.push(t); return Promise.resolve(); });
    create('setup');
    expect(auth.setupCalls).toEqual(['chal-1']);
    expect(el('twofa-setup-title')?.textContent).toContain('Set up two-factor sign-in');
    expect((el('twofa-qr') as HTMLImageElement).src).toBe(PNG);
    expect(el('twofa-secret')).toBeNull();
    expect(el('twofa-show-key')?.textContent).toContain("Can't scan? Show key");

    el('twofa-show-key')!.click(); fixture.detectChanges();
    expect(el('twofa-secret')?.textContent).toContain('JBSW Y3DP EHPK 3PXP');
    expect(el('twofa-show-key')?.getAttribute('aria-expanded')).toBe('true');
    el('twofa-copy-key')!.click(); tick(); fixture.detectChanges();
    expect(writes).toEqual(['JBSWY3DPEHPK3PXP']);
    expect(el('twofa-copy-key')?.textContent).toContain('Copied');
    tick(2000);
  }));

  it('auto-submits the sixth digit (digits only), then shows the recovery codes before continuing', fakeAsync(() => {
    auth.verifyAnswers.push(of({ ...SESSION, recoveryCodes: ['AAAAA-11111', 'BBBBB-22222'] } as LoginResponse));
    const c = create('setup');
    const done: LoginResponse[] = [];
    c.completed.subscribe((r) => done.push(r));
    const input = el('twofa-code') as HTMLInputElement;
    expect(input.getAttribute('autocomplete')).toBe('one-time-code');
    expect(input.getAttribute('inputmode')).toBe('numeric');

    type('12a3');
    expect(input.value).toBe('123');
    expect(auth.verifyCalls.length).toBe(0);
    expect((el('twofa-submit') as HTMLButtonElement).disabled).toBeTrue();
    type('123456');
    expect(auth.verifyCalls).toEqual([{ token: 'chal-1', code: '123456' }]);
    expect(el('twofa-recovery-codes')?.textContent).toContain('AAAAA-11111');
    expect(done.length).toBe(0);
    el('twofa-continue')!.click();
    expect(done.length).toBe(1);
    expect(done[0].accessToken).toBe('at');
    tick(100);
  }));

  it('a wrong code shows an error, clears the field and lets the user try again', fakeAsync(() => {
    auth.verifyAnswers.push(throwError(() => problem(401, 'The verification code is incorrect.')));
    const c = create('verify');
    const done: LoginResponse[] = [];
    c.completed.subscribe((r) => done.push(r));
    expect(auth.setupCalls.length).toBe(0);
    expect(el('twofa-verify-title')).not.toBeNull();
    expect(el('twofa-qr')).toBeNull();

    type('000000'); tick();
    expect(el('twofa-error')?.textContent).toContain("That code didn't work");
    expect((el('twofa-code') as HTMLInputElement).value).toBe('');
    type('654321'); tick();
    expect(auth.verifyCalls.map((v) => v.code)).toEqual(['000000', '654321']);
    expect(done.length).toBe(1);
  }));

  it('lockout (423) and an expired challenge have their own states', fakeAsync(() => {
    auth.verifyAnswers.push(throwError(() => problem(423, 'Locked.')), throwError(() => problem(401, 'This sign-in has expired. Sign in again.')));
    const c = create('verify');
    let cancelled = 0;
    c.cancel.subscribe(() => cancelled++);
    type('111111'); tick();
    expect(el('twofa-error')?.textContent).toContain('locked');
    type('222222'); tick();
    expect(el('twofa-code')).toBeNull();
    el('twofa-restart')!.click();
    expect(cancelled).toBe(1);
  }));

  it('verify: a recovery code is typed in full and does not auto-submit', fakeAsync(() => {
    create('verify');
    el('twofa-use-recovery')!.click(); fixture.detectChanges(); tick();
    const input = el('twofa-code') as HTMLInputElement;
    expect(input.getAttribute('inputmode')).toBe('text');
    type('ABCDE-12345');
    expect(auth.verifyCalls.length).toBe(0);
    el('twofa-submit')!.click(); fixture.detectChanges(); tick();
    expect(auth.verifyCalls).toEqual([{ token: 'chal-1', code: 'ABCDE-12345' }]);
  }));

  it('Arabic: translated strings, right-to-left page, and the key stays left-to-right', fakeAsync(() => {
    const i18n = TestBed.inject(I18nService);
    i18n.setLang('ar');
    TestBed.flushEffects();
    create('setup');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(el('twofa-setup-title')?.textContent).toContain('إعداد التحقق بخطوتين');
    expect(el('twofa-show-key')?.textContent).toContain('اعرض المفتاح');
    el('twofa-show-key')!.click(); fixture.detectChanges();
    expect(getComputedStyle(el('twofa-secret')!).direction).toBe('ltr');
    i18n.setLang('en');
    TestBed.flushEffects();
    tick(100);
  }));
});

describe('Login page with two-factor', () => {
  let auth: FakeAuth;
  let fixture: ComponentFixture<LoginPage>;
  const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(async () => {
    auth = new FakeAuth();
    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideNoopAnimations(), provideRouter([]), provideNzIcons(ICONS), { provide: AuthService, useValue: auth }],
    }).compileComponents();
    fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({ email: 'ana@acme.test', password: 'Secret#123', rememberMe: false, code: '' });
  });

  it('first sign-in: the password step hands over to the setup step, and Back returns to the form', fakeAsync(() => {
    auth.loginAnswer = of({ twoFactorSetupRequired: true, challengeToken: 'chal-setup', challengeExpiresIn: 600 } as LoginResponse);
    fixture.componentInstance.submit(); fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('login-email')).toBeNull();
    expect(el('twofa-setup-title')).not.toBeNull();
    expect(auth.setupCalls).toEqual(['chal-setup']);
    el('twofa-cancel')!.click(); fixture.detectChanges();
    expect(el('login-email')).not.toBeNull();
    expect(fixture.componentInstance.form.controls.password.value).toBe('');
    tick(100);
  }));

  it('returning user: the code step signs in and navigates', fakeAsync(() => {
    auth.loginAnswer = of({ requiresTwoFactor: true, twoFactorRequired: true, challengeToken: 'chal-verify', challengeExpiresIn: 600 } as LoginResponse);
    const nav = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture.componentInstance.submit(); fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('twofa-verify-title')).not.toBeNull();
    const input = el('twofa-code') as HTMLInputElement;
    input.value = '123456'; input.dispatchEvent(new Event('input')); fixture.detectChanges(); tick();
    expect(auth.verifyCalls).toEqual([{ token: 'chal-verify', code: '123456' }]);
    expect(nav).toHaveBeenCalled();
  }));

  it('no two-factor needed: signs in straight away', fakeAsync(() => {
    const nav = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture.componentInstance.submit(); tick();
    expect(nav).toHaveBeenCalled();
    expect(fixture.componentInstance.challenge()).toBeNull();
  }));
});
