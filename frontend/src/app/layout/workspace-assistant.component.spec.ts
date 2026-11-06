import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { Observable, Subject, of, throwError } from 'rxjs';
import { AssistantActionCard, AssistantStreamEvent, actionExpired } from '../core/api/assistant-stream';
import { WorkspaceAssistantApi } from '../core/api/workspace-assistant.api';
import { I18nService } from '../core/i18n/i18n.service';
import { MeResponse, SessionStore } from '../core/session/session.store';
import { APP_ICONS } from '../icons';
import { WorkspaceAssistantComponent, WorkspaceAssistantState, assistantErrorText, assistantStarters, assistantStatusText } from './workspace-assistant.component';

const me = (workspace: string, permissions = ['assistant.chats.use']): MeResponse => ({
  user: { id: 'u1', email: 'a@b.test', fullName: 'A', mustChangePassword: false },
  workspace: { id: workspace, code: workspace, name: workspace, countryCode: 'AE', baseCurrency: 'AED', timeZone: 'Asia/Dubai', defaultLocale: 'en' },
  workspaces: [], permissions, legalEntities: [], allLegalEntities: true, enabledModules: [{ key: 'assistant', uiRoute: '' }],
});

const card = (expiresAt: string | null, status: AssistantActionCard['status'] = 'pending'): AssistantActionCard => ({ id: 'c1', title: 'Add a new customer', lines: [{ label: 'Legal entity', value: 'Main' }], status, expiresAt });

describe('Workspace assistant', () => {
  describe('words', () => {
    it('maps every code the server sends, in both languages, and keeps the server text for anything else', () => {
      for (const code of ['user-limit', 'month-limit', 'daily-limit', 'no-service', 'off', 'interrupted', 'unavailable']) {
        expect(assistantErrorText('en', code, 'raw')).not.toBe('raw');
        expect(assistantErrorText('ar', code, 'raw')).toMatch(/[؀-ۿ]/);
      }
      expect(assistantErrorText('ar', 'something-new', 'Server words')).toBe('Server words');
    });

    it('translates what the assistant is doing', () => {
      for (const text of ['Thinking', 'Searching your records', 'Checking the help articles', 'Looking through your records', 'Running the report', 'Checking your overview', 'Checking approvals', 'Looking through the menu', 'Preparing the change']) {
        expect(assistantStatusText('en', text)).toBe(text);
        expect(assistantStatusText('ar', text)).toMatch(/[؀-ۿ]/);
      }
    });
  });

  describe('starters', () => {
    const everything = () => true;
    const general = ['What needs my attention today?', 'Who owes us the most?', 'Where do I add a new customer?'];

    it('picks by the page, in English and Arabic', () => {
      for (const [url, word] of [['/finance/invoices', 'invoices'], ['/sales/orders', 'sales'], ['/purchasing/orders', 'purchase'], ['/inventory/stock', 'stock'], ['/projects/list', 'project']]) {
        const starters = assistantStarters('en', url, everything);
        expect(starters.length).toBeGreaterThan(0);
        expect(starters.join(' ').toLowerCase()).toContain(word);
        expect(starters).not.toEqual(general);
        for (const s of assistantStarters('ar', url, everything)) expect(s).toMatch(/[؀-ۿ]/);
      }
    });

    it('reads only the path, and matches whole parts of it', () => {
      expect(assistantStarters('en', '/sales/orders?customer=Finance#top', everything)).toEqual(assistantStarters('en', '/sales', everything));
      expect(assistantStarters('en', '/dashboard?from=/finance/invoices', everything)).toEqual(general);
      expect(assistantStarters('en', '/sales-reports', everything)).toEqual(general);
      expect(assistantStarters('en', '/', everything)).toEqual(general);
    });

    it('offers nothing about HR, on any of its pages', () => {
      for (const url of ['/hr', '/hr/employees', '/hr/payroll?run=1']) {
        expect(assistantStarters('en', url, everything)).toEqual(general);
        expect(assistantStarters('ar', url, everything).length).toBe(3);
      }
    });

    it('shows only what the person can use, asking for the module and the permission', () => {
      const asked: [string, string][] = [];
      const onlyOrders = (module: string, permission: string) => { asked.push([module, permission]); return permission === 'sales.orders.read'; };
      expect(assistantStarters('en', '/sales/orders', onlyOrders)).toEqual(['Which sales orders are still open?']);
      expect(asked).toContain(['sales', 'sales.quotes.read']);
      expect(asked).toContain(['sales', 'sales.orders.read']);
    });

    it('falls back to the general ones when nothing on the page is theirs', () => {
      expect(assistantStarters('en', '/inventory/stock', () => false)).toEqual(general);
    });
  });

  describe('expiry', () => {
    const at = Date.parse('2026-11-06T12:00:00Z');
    it('counts only pending cards past their time', () => {
      expect(actionExpired(card('2026-11-06T11:59:59Z'), at)).toBeTrue();
      expect(actionExpired(card('2026-11-06T12:00:01Z'), at)).toBeFalse();
      expect(actionExpired(card(null), at)).toBeFalse();
      expect(actionExpired(card('2026-11-06T11:59:59Z', 'done'), at)).toBeFalse();
    });
  });

  describe('panel', () => {
    let asked: { language: string }[];
    let answer: Observable<AssistantStreamEvent>;
    let rated: [string, boolean | null][];
    let feedback: Observable<unknown>;

    const setUp = () => {
      localStorage.removeItem('erp.ui.prefs');
      asked = [];
      answer = of();
      rated = [];
      feedback = of(null);
      TestBed.configureTestingModule({
        imports: [WorkspaceAssistantComponent],
        providers: [
          provideRouter([]), provideNoopAnimations(), provideNzIcons(APP_ICONS),
          { provide: WorkspaceAssistantApi, useValue: {
            status: () => of({ available: true }), usage: () => of(null), conversations: () => of([]),
            ask: (input: { language: string }) => { asked.push(input); return answer; },
            feedback: (id: string, helpful: boolean | null) => { rated.push([id, helpful]); return feedback; },
          } },
        ],
      });
      const session = TestBed.inject(SessionStore);
      session.me.set(me('w1'));
      const fixture = TestBed.createComponent(WorkspaceAssistantComponent);
      TestBed.inject(WorkspaceAssistantState).open.set(true);
      fixture.detectChanges();
      return { fixture, session, component: fixture.componentInstance };
    };

    // The language is kept in the browser and on <html>, so it must not leak into other specs.
    afterEach(() => {
      TestBed.inject(I18nService).setLang('en');
      TestBed.flushEffects();
      localStorage.removeItem('erp.ui.prefs');
    });

    it('keeps the open chat when the session reloads, and clears it when the workspace changes', () => {
      const { fixture, session, component } = setUp();
      component.items.set([{ kind: 'user', text: 'Hello' }]);

      session.me.set(me('w1', ['assistant.chats.use', 'core.parties.read']));
      TestBed.flushEffects();
      expect(component.items().length).toBe(1);

      session.me.set(me('w2'));
      TestBed.flushEffects();
      fixture.detectChanges();
      expect(component.items().length).toBe(0);
    });

    it('hides Confirm once a card has expired', () => {
      const { fixture, component } = setUp();
      const element = fixture.nativeElement as HTMLElement;

      component.items.set([{ kind: 'action', card: card(new Date(Date.now() + 60_000).toISOString()) }]);
      fixture.detectChanges();
      expect(element.querySelector('[data-testid="workspace-assistant-confirm"]')).not.toBeNull();

      component.items.set([{ kind: 'action', card: card('2000-01-01T00:00:00Z') }]);
      fixture.detectChanges();
      expect(element.querySelector('[data-testid="workspace-assistant-confirm"]')).toBeNull();
      expect(element.querySelector('[data-testid="workspace-assistant-card-expired"]')).not.toBeNull();
      expect(element.querySelector('[data-testid="workspace-assistant-outcome"]')?.textContent).toContain('Expired');
    });

    it('offers the thumbs only once the reply has finished, with names in both languages', () => {
      const { fixture, component } = setUp();
      const element = fixture.nativeElement as HTMLElement;
      const stream = new Subject<AssistantStreamEvent>();
      answer = stream;
      const helpful = () => element.querySelector<HTMLButtonElement>('[data-testid="workspace-assistant-helpful"]');

      component.send('Hello');
      stream.next({ type: 'delta', text: 'Hi' });
      stream.next({ type: 'reply', id: 'r1' });
      stream.next({ type: 'done', inputTokens: 1, outputTokens: 1 });
      fixture.detectChanges();
      expect(helpful()).toBeNull(); // the server saves the row as the stream ends

      stream.complete();
      fixture.detectChanges();
      expect(helpful()?.getAttribute('aria-label')).toBe('This answer was helpful');
      expect(helpful()?.getAttribute('aria-pressed')).toBe('false');
      expect(element.querySelector('[data-testid="workspace-assistant-not-helpful"]')?.getAttribute('aria-label')).toBe('This answer was not helpful');

      TestBed.inject(I18nService).setLang('ar');
      fixture.detectChanges();
      expect(helpful()?.getAttribute('aria-label')).toMatch(/[؀-ۿ]/);
      expect(element.querySelector('[data-testid="workspace-assistant-not-helpful"]')?.getAttribute('aria-label')).toMatch(/[؀-ۿ]/);
    });

    it('sends a choice, takes it back on a second press, and restores it on a failure', () => {
      const { fixture, component } = setUp();
      const element = fixture.nativeElement as HTMLElement;
      component.items.set([{ kind: 'assistant', text: 'Hi', id: 'r1', helpful: null }]);
      fixture.detectChanges();
      const press = () => { element.querySelector<HTMLButtonElement>('[data-testid="workspace-assistant-not-helpful"]')!.click(); fixture.detectChanges(); };
      const pressed = () => element.querySelector('[data-testid="workspace-assistant-not-helpful"]')?.getAttribute('aria-pressed');

      press();
      expect(rated).toEqual([['r1', false]]);
      expect(pressed()).toBe('true');

      press();
      expect(rated[1]).toEqual(['r1', null]);
      expect(pressed()).toBe('false');

      feedback = throwError(() => new Error('down'));
      press();
      expect(pressed()).toBe('false');
    });

    it('shows a saved choice when a past chat opens, and no thumbs on a reply without an id', () => {
      const { fixture, component } = setUp();
      const element = fixture.nativeElement as HTMLElement;
      component.items.set([{ kind: 'assistant', text: 'Stopped part way' }]);
      fixture.detectChanges();
      expect(element.querySelector('[data-testid="workspace-assistant-helpful"]')).toBeNull();

      TestBed.inject(WorkspaceAssistantApi).conversation = () => of({ id: 'c1', title: 'T', actions: [], messages: [{ id: 'r9', at: '2026-11-06T10:00:00Z', fromUser: false, text: 'Saved', helpful: true }] });
      component.openConversation('c1');
      fixture.detectChanges();
      expect(element.querySelector('[data-testid="workspace-assistant-helpful"]')?.getAttribute('aria-pressed')).toBe('true');
    });

    it('sends the language and shows a limit in that language', () => {
      const { component } = setUp();
      TestBed.inject(I18nService).setLang('ar');
      answer = of({ type: 'error', code: 'user-limit', message: 'You have used today\'s messages.' });

      component.send('مرحبا');

      expect(asked[0].language).toBe('ar');
      expect(component.error()).toMatch(/[؀-ۿ]/);
    });
  });
});
