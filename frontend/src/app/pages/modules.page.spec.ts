import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { NzModalService } from 'ng-zorro-antd/modal';
import { of } from 'rxjs';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { I18nService } from '../core/i18n/i18n.service';
import { SessionStore, MeResponse } from '../core/session/session.store';
import { ModulesPage } from './modules.page';

const row = (key: string, state: string, notice: string | null = null) => ({ key, name: key, description: 'About', category: 'Horizontal', version: '1.0', isCore: false, requires: [], uiRoute: '', state, notice });

describe('Modules page', () => {
  let http: HttpTestingController;

  const open = (modules: unknown[]) => {
    const fixture = TestBed.createComponent(ModulesPage);
    http.expectOne('/api/v1/modules').flush(modules);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement, confirm: spyOn((fixture.componentInstance as unknown as { modal: NzModalService }).modal, 'confirm') };
  };

  beforeEach(() => {
    localStorage.removeItem('erp.ui.prefs');
    TestBed.configureTestingModule({
      imports: [ModulesPage],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(),
        { provide: API_BASE_URL, useValue: '/api/v1' },
        { provide: AuthService, useValue: { refresh: () => of(null), loadMe: () => of(null) } },
      ],
    });
    TestBed.inject(SessionStore).me.set({ permissions: ['*'], enabledModules: [] } as unknown as MeResponse);
    http = TestBed.inject(HttpTestingController);
  });

  // The language is kept in the browser and on <html>, so it must not leak into other specs.
  afterEach(() => {
    TestBed.inject(I18nService).setLang('en');
    TestBed.flushEffects();
    localStorage.removeItem('erp.ui.prefs');
    http.verify();
  });

  it('asks before enabling the assistant, and changes nothing until the administrator agrees', () => {
    const { fixture, confirm } = open([row('assistant', 'disabled')]);

    fixture.componentInstance.toggle(fixture.componentInstance.modules()[0]);
    http.expectNone('/api/v1/modules/assistant/enable');
    expect(confirm).toHaveBeenCalledTimes(1);
    const options = confirm.calls.mostRecent().args[0]!;
    expect(String(options.nzContent)).toContain('outside AI provider');

    (options.nzOnOk as () => void)();
    http.expectOne('/api/v1/modules/assistant/enable').flush({});
    http.expectOne('/api/v1/modules').flush([]);
  });

  it('writes the notice in the language of the page', () => {
    TestBed.inject(I18nService).setLang('ar');
    const { fixture, confirm } = open([row('assistant', 'disabled')]);

    fixture.componentInstance.toggle(fixture.componentInstance.modules()[0]);

    expect(String(confirm.calls.mostRecent().args[0]!.nzContent)).toMatch(/[؀-ۿ]/);
  });

  it('does not ask for other modules, or when turning the assistant off', () => {
    const { fixture, confirm } = open([row('sales', 'disabled'), row('assistant', 'enabled')]);

    fixture.componentInstance.toggle(fixture.componentInstance.modules()[0]);
    http.expectOne('/api/v1/modules/sales/enable').flush({});
    http.expectOne('/api/v1/modules').flush([row('sales', 'enabled'), row('assistant', 'enabled')]);

    fixture.componentInstance.toggle(fixture.componentInstance.modules()[1]);
    http.expectOne('/api/v1/modules/assistant/disable').flush({});
    http.expectOne('/api/v1/modules').flush([]);
    expect(confirm).not.toHaveBeenCalled();
  });

  for (const [code, words] of [['assistant-off', 'switched off'], ['assistant-no-service', 'not ready'], ['assistant-plan', 'plan']]) {
    it(`says why the assistant stays hidden: ${code}`, () => {
      const { element } = open([row('assistant', 'enabled', code), row('sales', 'enabled')]);
      expect(element.querySelector('[data-testid="module-notice-assistant"]')?.textContent).toContain(words);
      expect(element.querySelector('[data-testid="module-notice-sales"]')).toBeNull();
    });
  }

  it('shows nothing when the assistant is ready', () => {
    const { element } = open([row('assistant', 'enabled')]);
    expect(element.querySelector('[data-testid="module-notice-assistant"]')).toBeNull();
  });
});
