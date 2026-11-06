import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { API_BASE_URL } from '../core/api/api-config';
import { optimistic } from '../core/http/optimistic';
import { I18nService } from '../core/i18n/i18n.service';
import { PreferenceSwitchesComponent } from '../shared/preference-switches.component';
import { NotificationDrawerComponent } from './notification-drawer.component';
import { PlatformShellComponent } from './platform-shell.component';
import { ShellComponent } from './shell.component';

describe('profile menu', () => {
  const providers = [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), { provide: API_BASE_URL, useValue: '/api/v1' }];
  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;

  const startPlain = () => { const theme = TestBed.inject(TenantThemeService); theme.setMode('light'); theme.setLanguage('en'); };

  beforeEach(() => { TestBed.configureTestingModule({ providers }); startPlain(); });
  afterEach(() => { startPlain(); TestBed.tick(); overlay()?.replaceChildren(); });

  for (const [name, component, menu, lang, theme] of [
    ['workspace shell', ShellComponent, 'user-menu', 'lang-toggle', 'theme-toggle'],
    ['platform console', PlatformShellComponent, 'platform-user-menu', 'platform-lang-toggle', 'platform-theme-toggle'],
  ] as const) {
    describe(name, () => {
      let fixture: ComponentFixture<unknown>;
      // ng-zorro opens its dropdown on a real timer outside Angular.
      const open = async () => {
        (fixture.nativeElement.querySelector(`[data-testid="${menu}"]`) as HTMLElement).click();
        for (let waited = 0; waited < 2000 && !item(lang); waited += 50) {
          await new Promise((r) => setTimeout(r, 50));
          fixture.detectChanges();
        }
      };
      const item = (id: string) => document.querySelector(`.cdk-overlay-container [data-testid="${id}"]`) as HTMLElement;

      beforeEach(() => {
        fixture = TestBed.createComponent(component as never);
        fixture.detectChanges();
      });

      it('has neither control in the top bar', () => {
        expect(fixture.nativeElement.querySelector(`[data-testid="${lang}"]`)).toBeNull();
        expect(fixture.nativeElement.querySelector(`[data-testid="${theme}"]`)).toBeNull();
      });

      it('switches the language from the menu', async () => {
        await open();
        expect(item(lang).textContent).toContain('العربية');
        item(lang).click();
        expect(TestBed.inject(I18nService).lang()).toBe('ar');
      });

      it('switches the theme from the menu', async () => {
        await open();
        expect(item(theme).textContent).toContain('Dark mode');
        item(theme).click();
        expect(TestBed.inject(TenantThemeService).resolvedTheme()).toBe('dark');
      });
    });
  }

  describe('signed-out pages', () => {
    it('offer the same two choices', () => {
      const f = TestBed.createComponent(PreferenceSwitchesComponent);
      f.detectChanges();
      f.nativeElement.querySelector('[data-testid="public-lang-toggle"]').click();
      f.nativeElement.querySelector('[data-testid="public-theme-toggle"]').click();
      expect(TestBed.inject(I18nService).lang()).toBe('ar');
      expect(TestBed.inject(TenantThemeService).resolvedTheme()).toBe('dark');
    });
  });

  describe('notifications', () => {
    const notice = { id: 'n1', title: 'Hello', createdAt: new Date().toISOString(), readAt: null };
    const setup = () => {
      const f = TestBed.createComponent(NotificationDrawerComponent);
      f.componentRef.setInput('open', true);
      f.detectChanges();
      const http = TestBed.inject(HttpTestingController);
      expect(f.nativeElement.querySelector('[data-testid="notifications-loading"]')).not.toBeNull();
      http.expectOne('/api/v1/notifications').flush({ items: [notice] });
      f.detectChanges();
      const shifts: number[] = [];
      f.componentInstance.unreadShift.subscribe((n) => shifts.push(n));
      return { f, http, shifts };
    };

    it('shows the shape of the list, not "all caught up", until it has loaded', () => {
      const { f } = setup();
      expect(f.nativeElement.querySelector('[data-testid="notifications-loading"]')).toBeNull();
      expect(f.nativeElement.querySelector('[data-testid="notification-item"]')).not.toBeNull();
    });

    it('marks everything read before the server answers', () => {
      const { f, http, shifts } = setup();
      f.componentInstance.markAll();
      expect(f.componentInstance.unreadCount()).toBe(0);
      expect(shifts).toEqual([-1]);
      http.expectOne('/api/v1/notifications/read-all').flush({});
      expect(f.componentInstance.unreadCount()).toBe(0);
    });

    it('puts the marks back if the server refuses', () => {
      const { f, http, shifts } = setup();
      f.componentInstance.markAll();
      http.expectOne('/api/v1/notifications/read-all').flush({}, { status: 500, statusText: 'x' });
      expect(f.componentInstance.unreadCount()).toBe(1);
      expect(shifts).toEqual([-1, 1]);
    });
  });

  describe('optimistic()', () => {
    it('applies first and leaves it applied on success', () => {
      const log: string[] = [];
      optimistic(of(1), { apply: () => log.push('apply'), rollback: () => log.push('rollback'), done: () => log.push('done') });
      expect(log).toEqual(['apply', 'done']);
    });

    it('rolls back on failure', () => {
      const log: string[] = [];
      optimistic(throwError(() => new Error('no')), { apply: () => log.push('apply'), rollback: () => log.push('rollback'), failed: () => log.push('failed') });
      expect(log).toEqual(['apply', 'rollback', 'failed']);
    });
  });
});
