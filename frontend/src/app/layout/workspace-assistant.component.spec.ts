import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { Observable, of } from 'rxjs';
import { AssistantActionCard, AssistantStreamEvent, actionExpired } from '../core/api/assistant-stream';
import { WorkspaceAssistantApi } from '../core/api/workspace-assistant.api';
import { I18nService } from '../core/i18n/i18n.service';
import { MeResponse, SessionStore } from '../core/session/session.store';
import { APP_ICONS } from '../icons';
import { WorkspaceAssistantComponent, WorkspaceAssistantState, assistantErrorText, assistantStatusText } from './workspace-assistant.component';

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

    const setUp = () => {
      localStorage.removeItem('erp.ui.prefs');
      asked = [];
      answer = of();
      TestBed.configureTestingModule({
        imports: [WorkspaceAssistantComponent],
        providers: [
          provideRouter([]), provideNoopAnimations(), provideNzIcons(APP_ICONS),
          { provide: WorkspaceAssistantApi, useValue: {
            status: () => of({ available: true }), usage: () => of(null), conversations: () => of([]),
            ask: (input: { language: string }) => { asked.push(input); return answer; },
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
