import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { API_BASE_URL } from '../core/api/api-config';
import { I18nService } from '../core/i18n/i18n.service';
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
});
