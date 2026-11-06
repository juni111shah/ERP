import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { Subject, of, throwError } from 'rxjs';
import { AssistantStreamEvent } from '../core/api/assistant-stream';
import { PlatformApi } from '../core/api/platform.api';
import { APP_ICONS } from '../icons';
import { BrandAssistantComponent, assistantErrorText, leadErrorText, statusText } from './brand-assistant.component';

describe('brand assistant feedback', () => {
  const setUp = (feedback: () => unknown = () => of(null)) => {
    sessionStorage.clear();
    sessionStorage.setItem('erp.brandAssistant.lang', 'en');
    const stream = new Subject<AssistantStreamEvent>();
    const sent: unknown[][] = [];
    TestBed.configureTestingModule({
      imports: [BrandAssistantComponent],
      providers: [provideRouter([]), provideNoopAnimations(), provideNzIcons(APP_ICONS), { provide: PlatformApi, useValue: {
        brandAssistantStatus: () => of({ visible: true, supportEmail: null }),
        askBrandAssistant: () => stream,
        brandFeedback: (...args: unknown[]) => { sent.push(args); return feedback(); },
      } }],
    });
    const fixture = TestBed.createComponent(BrandAssistantComponent);
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const button = (name: string) => element.querySelector<HTMLButtonElement>(`[data-testid="brand-assistant-${name}"]`);
    return { fixture, component: fixture.componentInstance, stream, sent, button };
  };
  afterEach(() => sessionStorage.clear());

  it('offers the thumbs once the reply has finished, even with a contact offer after the words', () => {
    const { fixture, component, stream, button } = setUp();
    component.send('How do I pay?');
    stream.next({ type: 'session', token: 'tok' });
    stream.next({ type: 'delta', text: 'By bank transfer.' });
    stream.next({ type: 'reply', id: 'r1' });
    stream.next({ type: 'contact', reason: 'unavailable' });
    fixture.detectChanges();
    expect(button('helpful')).toBeNull();

    stream.complete();
    fixture.detectChanges();
    expect(button('helpful')?.getAttribute('aria-label')).toBe('This answer was helpful');
    expect(component.messages().find((m) => m.id === 'r1')?.text).toBe('By bank transfer.');
  });

  it('sends the session token, keeps the choice across a page change, and names the buttons in Arabic', () => {
    const { fixture, component, stream, sent, button } = setUp();
    component.send('Hello');
    stream.next({ type: 'session', token: 'tok' });
    stream.next({ type: 'delta', text: 'Hi' });
    stream.next({ type: 'reply', id: 'r1' });
    stream.complete();
    fixture.detectChanges();

    button('not-helpful')!.click();
    fixture.detectChanges();
    expect(sent).toEqual([['r1', { token: 'tok', helpful: false }]]);
    expect(button('not-helpful')?.getAttribute('aria-pressed')).toBe('true');
    expect(JSON.parse(sessionStorage.getItem('erp.brandAssistant.messages')!).find((m: { id?: string }) => m.id === 'r1').helpful).toBeFalse();

    button('not-helpful')!.click();
    fixture.detectChanges();
    expect(sent[1]).toEqual(['r1', { token: 'tok', helpful: null }]);

    component.toggleLang();
    fixture.detectChanges();
    expect(button('helpful')?.getAttribute('aria-label')).toMatch(/[؀-ۿ]/);
    expect(button('not-helpful')?.getAttribute('aria-label')).toMatch(/[؀-ۿ]/);
  });

  it('puts the choice back when the rate limit refuses it, and says why', () => {
    const { fixture, component, stream, button } = setUp(() => throwError(() => new HttpErrorResponse({ status: 429 })));
    component.send('Hello');
    stream.next({ type: 'delta', text: 'Hi' });
    stream.next({ type: 'reply', id: 'r1' });
    stream.complete();
    fixture.detectChanges();

    button('helpful')!.click();
    fixture.detectChanges();
    expect(button('helpful')?.getAttribute('aria-pressed')).toBe('false');
    expect(component.error()).toBe('You have sent a lot of questions. Please try again a little later.');
  });
});

describe('brand assistant wording', () => {
  it('translates what the server says it is doing, and shows anything new as written', () => {
    expect(statusText('ar', 'Checking the plans')).toBe('جارٍ مراجعة الخطط');
    expect(statusText('ar', 'Reading the policy')).toBe('جارٍ قراءة السياسة');
    expect(statusText('ar', 'Doing something new')).toBe('Doing something new');
    expect(statusText('en', 'Checking the plans')).toBe('Checking the plans');
  });

  it('has its own words for every limit code in both languages', () => {
    for (const code of ['daily-limit', 'no-service', 'unavailable', 'interrupted']) {
      expect(assistantErrorText('en', code, 'server words')).not.toBe('server words');
      expect(assistantErrorText('ar', code, 'server words')).not.toBe('server words');
      expect(assistantErrorText('ar', code, '')).toMatch(/[؀-ۿ]/);
    }
  });

  it('falls back to the server words for a code it does not know, then to its own', () => {
    expect(assistantErrorText('ar', 'new-code', 'server words')).toBe('server words');
    expect(assistantErrorText('ar', 'new-code', '')).toBe('عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.');
  });

  const refused = (errors: Record<string, string[]>) => new HttpErrorResponse({ status: 400, error: { title: 'Validation failed', errors } });

  it('translates a refused contact form by field', () => {
    expect(leadErrorText('ar', refused({ 'Input.Email': ['Enter a valid email address.'] }))).toBe('أدخل عنوان بريد إلكتروني صحيحاً.');
    expect(leadErrorText('ar', refused({ 'Input.Consent': ['Please agree so that we can contact you.'] }))).toBe('يرجى الموافقة حتى نتمكن من التواصل معك.');
    expect(leadErrorText('en', refused({ 'Input.Email': ['Enter a valid email address.'] }))).toBe('Enter a valid email address.');
  });

  it('shows the server words for a field it has no translation for', () => {
    expect(leadErrorText('ar', refused({ 'Input.Phone': ['Too long.'] }))).toBe('Too long.');
  });

  it('says the visitor is sending too much when the rate limit answers', () => {
    expect(leadErrorText('ar', new HttpErrorResponse({ status: 429 }))).toBe('لقد أرسلت أسئلة كثيرة. يرجى المحاولة لاحقاً.');
    expect(leadErrorText('en', new HttpErrorResponse({ status: 429 }))).toBe('You have sent a lot of questions. Please try again a little later.');
  });
});
