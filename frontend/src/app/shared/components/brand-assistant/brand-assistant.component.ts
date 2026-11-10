import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { Subscription, catchError, of } from 'rxjs';
import { streamErrorMessage } from '@core/api/assistant-stream';
import { PlatformApi, PublicPlan } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { AssistantTextComponent } from '../assistant-text/assistant-text.component';

type Lang = 'en' | 'ar';
interface Message { role: 'visitor' | 'assistant'; text: string; plans?: PublicPlan[]; canStart?: boolean; contact?: boolean; }

const TOKEN_KEY = 'erp.brandAssistant.token';
const MESSAGES_KEY = 'erp.brandAssistant.messages';
const LANG_KEY = 'erp.brandAssistant.lang';

const WORDS = {
  en: {
    open: 'Ask about HigherSquare 360', title: 'HigherSquare assistant', close: 'Close the assistant', language: 'Arabic', languageShort: 'ع',
    notice: 'AI answers can be wrong, so check anything important. Please do not share passwords or payment details.',
    privacy: 'Privacy', hello: 'Hi! Ask me about plans, modules or getting started.', placeholder: 'Type your question', send: 'Send', stop: 'Stop', thinking: 'Thinking',
    failed: 'Sorry, something went wrong. Please try again.', busy: 'You have sent a lot of questions. Please try again a little later.',
    contact: 'Talk to our team', contactTitle: 'Talk to our team', name: 'Your name', email: 'Work email', phone: 'Phone (optional)', company: 'Company (optional)', need: 'How can we help? (optional)',
    consent: 'I agree that you may contact me about this.', submit: 'Send my details', back: 'Back to the chat', sent: 'Thanks. Our team will contact you by email.',
    start: 'Start free trial', choose: 'Choose this plan', compare: 'Compare plans', perMonth: 'per month', users: (n: number) => `Up to ${n} users`, noLimit: 'No limit on users',
    suggestions: ['Which plan fits 12 users?', 'What does the free trial include?', 'Which modules can I use?'],
  },
  ar: {
    open: 'اسأل عن HigherSquare 360', title: 'مساعد HigherSquare', close: 'إغلاق المساعد', language: 'English', languageShort: 'EN',
    notice: 'قد تكون إجابات الذكاء الاصطناعي خاطئة، لذا تحقق من أي أمر مهم. يرجى عدم مشاركة كلمات المرور أو بيانات الدفع.',
    privacy: 'الخصوصية', hello: 'مرحباً! اسألني عن الخطط أو الوحدات أو كيفية البدء.', placeholder: 'اكتب سؤالك', send: 'إرسال', stop: 'إيقاف', thinking: 'جارٍ التفكير',
    failed: 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.', busy: 'لقد أرسلت أسئلة كثيرة. يرجى المحاولة لاحقاً.',
    contact: 'تحدث مع فريقنا', contactTitle: 'تحدث مع فريقنا', name: 'اسمك', email: 'بريد العمل', phone: 'الهاتف (اختياري)', company: 'الشركة (اختياري)', need: 'كيف يمكننا مساعدتك؟ (اختياري)',
    consent: 'أوافق على أن تتواصلوا معي بخصوص هذا الأمر.', submit: 'إرسال بياناتي', back: 'العودة إلى المحادثة', sent: 'شكراً لك. سيتواصل معك فريقنا عبر البريد الإلكتروني.',
    start: 'ابدأ التجربة المجانية', choose: 'اختر هذه الخطة', compare: 'قارن الخطط', perMonth: 'شهرياً', users: (n: number) => `حتى ${n} مستخدم`, noLimit: 'بلا حد للمستخدمين',
    suggestions: ['أي خطة تناسب 12 مستخدماً؟', 'ماذا تشمل الفترة التجريبية المجانية؟', 'ما الوحدات التي يمكنني استخدامها؟'],
  },
};

/** The assistant on the public pages. It shows nothing unless the founder has switched it on, and it knows only public facts. */
@Component({
  selector: 'erp-brand-assistant',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzCheckboxModule, NzIconModule, NzInputModule, AssistantTextComponent],
  styles: [`
    .launcher { position: fixed; inset-block-end: 20px; inset-inline-end: 20px; z-index: 1000; width: 60px; height: 60px; padding: 0; border: 0; border-radius: 50%; background: #fff; cursor: pointer; box-shadow: 0 8px 24px rgba(0,0,0,.24); transition: transform .15s ease; }
    .launcher:hover { transform: scale(1.06); }
    .launcher img { display: block; width: 100%; height: 100%; }
    .head img { width: 28px; height: 28px; flex-shrink: 0; }
    .launcher:focus-visible, .head button:focus-visible, .chips button:focus-visible { outline: 2px solid var(--erp-color-focus-ring, #10955d); outline-offset: 2px; }
    .panel { position: fixed; inset-block-end: 20px; inset-inline-end: 20px; z-index: 1000; width: 400px; max-width: calc(100vw - 24px); height: min(640px, calc(100vh - 40px)); display: flex; flex-direction: column; background: var(--erp-color-bg-surface, #fff); color: var(--erp-color-text-primary, #0f172a); border: 1px solid var(--erp-color-border-subtle, #e2e8f0); border-radius: 14px; box-shadow: 0 16px 48px rgba(0,0,0,.22); overflow: hidden; font-size: 14px; }
    .head { display: flex; align-items: center; gap: 8px; padding: 12px 14px; border-bottom: 1px solid var(--erp-color-border-subtle, #e2e8f0); }
    .head strong { flex: 1; min-width: 0; }
    .head button { border: 1px solid var(--erp-color-border-subtle, #e2e8f0); background: none; border-radius: 8px; min-width: 32px; height: 32px; cursor: pointer; color: inherit; font: inherit; }
    .notice { padding: 8px 14px; font-size: 12px; color: var(--erp-color-text-muted, #64748b); background: var(--erp-color-bg-subtle, #f8fafc); }
    .notice a { color: inherit; text-decoration: underline; }
    .log { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 14px; display: flex; flex-direction: column; gap: 8px; }
    .bubble { max-width: 88%; padding: 8px 12px; border-radius: 12px; }
    .bubble.visitor { align-self: flex-end; background: var(--erp-color-primary-subtle, #e7f6ef); white-space: pre-wrap; overflow-wrap: anywhere; }
    .bubble.assistant { align-self: flex-start; background: var(--erp-color-bg-subtle, #f1f5f9); }
    .status { color: var(--erp-color-text-muted, #64748b); font-size: 12.5px; }
    .chips { display: flex; flex-direction: column; align-items: flex-start; gap: 6px; }
    .chips button { border: 1px solid var(--erp-color-border-subtle, #e2e8f0); background: none; border-radius: 16px; padding: 5px 12px; cursor: pointer; color: inherit; font: inherit; text-align: start; }
    .plans { display: flex; flex-direction: column; gap: 6px; width: 100%; }
    .plan { border: 1px solid var(--erp-color-border-subtle, #e2e8f0); border-radius: 10px; padding: 8px 12px; display: flex; align-items: center; justify-content: space-between; gap: 8px; flex-wrap: wrap; }
    .plan small { display: block; color: var(--erp-color-text-muted, #64748b); }
    .contact-link { align-self: flex-start; }
    .send { display: flex; gap: 8px; align-items: flex-end; padding: 10px 14px; border-top: 1px solid var(--erp-color-border-subtle, #e2e8f0); }
    .send textarea { flex: 1; min-width: 0; resize: none; }
    .form { flex: 1; min-height: 0; overflow-y: auto; padding: 14px; display: flex; flex-direction: column; gap: 10px; }
    .form label { display: block; font-size: 13px; }
    .form input, .form textarea { margin-top: 4px; }
    .error { color: var(--erp-color-error-text, #b91c1c); font-size: 13px; }
    .trap { position: absolute; inset-inline-start: -10000px; width: 1px; height: 1px; overflow: hidden; }
    @media (max-width: 480px) {
      .panel { inset: 0; width: 100%; max-width: 100%; height: 100%; border-radius: 0; border: 0; }
    }
  `],
  template: `
    @if (status()?.visible) {
      @if (!open()) {
        <button type="button" class="launcher" [attr.dir]="dir()" [attr.aria-label]="w().open" [attr.title]="w().open" (click)="show()" data-testid="brand-assistant-open"><img src="highersquare-assistant.svg" alt="" /></button>
      } @else {
        <!-- data-no-i18n: the page translator must never rewrite what the visitor or the model wrote. -->
        <section class="panel" role="dialog" [attr.aria-label]="w().title" [attr.dir]="dir()" [attr.lang]="lang()" data-no-i18n (keydown.escape)="open.set(false)" data-testid="brand-assistant">
          <div class="head">
            <img src="highersquare-assistant.svg" alt="" />
            <strong>{{ w().title }}</strong>
            <button type="button" (click)="toggleLang()" [attr.aria-label]="w().language" data-testid="brand-assistant-lang">{{ w().languageShort }}</button>
            <button type="button" (click)="open.set(false)" [attr.aria-label]="w().close" data-testid="brand-assistant-close"><span nz-icon nzType="close" aria-hidden="true"></span></button>
          </div>
          <div class="notice">{{ w().notice }} <a routerLink="/privacy">{{ w().privacy }}</a></div>

          @if (view() === 'contact') {
            <form class="form" (ngSubmit)="submitLead()" data-testid="brand-lead-form">
              <strong>{{ w().contactTitle }}</strong>
              <label>{{ w().name }}<input nz-input name="name" required maxlength="120" autocomplete="name" [ngModel]="lead().name" (ngModelChange)="setLead('name', $event)" data-testid="brand-lead-name" /></label>
              <label>{{ w().email }}<input nz-input type="email" name="email" required maxlength="256" autocomplete="email" dir="ltr" [ngModel]="lead().email" (ngModelChange)="setLead('email', $event)" data-testid="brand-lead-email" /></label>
              <label>{{ w().phone }}<input nz-input type="tel" name="phone" maxlength="40" autocomplete="tel" dir="ltr" [ngModel]="lead().phone" (ngModelChange)="setLead('phone', $event)" /></label>
              <label>{{ w().company }}<input nz-input name="company" maxlength="200" autocomplete="organization" [ngModel]="lead().company" (ngModelChange)="setLead('company', $event)" /></label>
              <label>{{ w().need }}<textarea nz-input name="need" rows="3" maxlength="1000" dir="auto" [ngModel]="lead().need" (ngModelChange)="setLead('need', $event)"></textarea></label>
              <div class="trap" aria-hidden="true"><label>Website<input name="website" tabindex="-1" autocomplete="off" [ngModel]="trap()" (ngModelChange)="trap.set($event)" /></label></div>
              <label nz-checkbox name="consent" [ngModel]="lead().consent" (ngModelChange)="setLead('consent', $event)" data-testid="brand-lead-consent">{{ w().consent }}</label>
              @if (leadError(); as e) { <div class="error" role="alert" data-testid="brand-lead-error">{{ e }}</div> }
              <button nz-button nzType="primary" [nzLoading]="leadSaving()" data-testid="brand-lead-submit">{{ w().submit }}</button>
              <button nz-button type="button" (click)="view.set('chat')">{{ w().back }}</button>
            </form>
          } @else {
            <div class="log" #log aria-live="polite" data-testid="brand-assistant-log">
              @if (!messages().length) {
                <div class="bubble assistant">{{ w().hello }}</div>
                <div class="chips">
                  @for (s of w().suggestions; track s) { <button type="button" (click)="send(s)" data-testid="brand-assistant-suggestion">{{ s }}</button> }
                </div>
              }
              @for (m of messages(); track $index) {
                @if (m.plans?.length) {
                  <div class="plans" data-testid="brand-assistant-plans">
                    @for (p of m.plans; track p.id) {
                      <div class="plan">
                        <div dir="auto"><strong>{{ p.name }}</strong><small><bdi>{{ money(p.monthlyPrice, p) }}</bdi> {{ w().perMonth }} · {{ p.maxUsers ? w().users(p.maxUsers) : w().noLimit }}</small></div>
                        @if (m.canStart) { <a nz-button nzSize="small" routerLink="/register" [queryParams]="{ plan: p.code, cycle: 'monthly' }" (click)="open.set(false)">{{ p.trialDays > 0 ? w().start : w().choose }}</a> }
                      </div>
                    }
                  </div>
                }
                @if (m.text) {
                  <div class="bubble" [class.visitor]="m.role === 'visitor'" [class.assistant]="m.role === 'assistant'" [attr.data-testid]="'brand-assistant-' + m.role">
                    @if (m.role === 'visitor') { <span dir="auto">{{ m.text }}</span> } @else { <erp-assistant-text [text]="m.text" (navigated)="open.set(false)" /> }
                  </div>
                }
                @if (m.contact) { <button nz-button nzSize="small" type="button" class="contact-link" (click)="view.set('contact')" data-testid="brand-assistant-contact-offer">{{ w().contact }}</button> }
              }
              @if (thinking()) { <div class="status" data-testid="brand-assistant-status">{{ thinking() }}...</div> }
              @if (error(); as e) { <div class="error" role="alert" data-testid="brand-assistant-error">{{ e }}</div> }
              @if (leadSent()) { <div class="bubble assistant" data-testid="brand-lead-sent">{{ w().sent }}</div> }
            </div>
            <div class="send">
              <textarea #box nz-input rows="2" maxlength="1000" dir="auto" [attr.aria-label]="w().placeholder" [placeholder]="w().placeholder" [ngModel]="draft()" (ngModelChange)="draft.set($event)" (keydown.enter)="enter($any($event))" data-testid="brand-assistant-input"></textarea>
              @if (sending()) {
                <button nz-button type="button" (click)="stop()" data-testid="brand-assistant-stop">{{ w().stop }}</button>
              } @else {
                <button nz-button nzType="primary" type="button" [disabled]="!draft().trim()" (click)="send(draft())" data-testid="brand-assistant-send">{{ w().send }}</button>
              }
            </div>
            @if (!leadSent()) { <div style="padding:0 14px 10px"><a role="button" tabindex="0" style="font-size:12.5px;text-decoration:underline;cursor:pointer" (click)="view.set('contact')" (keydown.enter)="view.set('contact')" data-testid="brand-assistant-contact">{{ w().contact }}</a></div> }
          }
        </section>
      }
    }`,
})
export class BrandAssistantComponent {
  private readonly api = inject(PlatformApi);
  private readonly logRef = viewChild<ElementRef<HTMLElement>>('log');
  private readonly boxRef = viewChild<ElementRef<HTMLTextAreaElement>>('box');

  readonly status = toSignal(this.api.brandAssistantStatus().pipe(catchError(() => of(null))), { initialValue: null });
  readonly open = signal(false);
  readonly view = signal<'chat' | 'contact'>('chat');
  readonly lang = signal<Lang>(this.initialLang());
  readonly dir = computed(() => (this.lang() === 'ar' ? 'rtl' : 'ltr'));
  readonly w = computed(() => WORDS[this.lang()]);
  readonly messages = signal<Message[]>(this.savedMessages());
  readonly draft = signal('');
  readonly sending = signal(false);
  readonly thinking = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly lead = signal({ name: '', email: '', phone: '', company: '', need: '', consent: false });
  readonly trap = signal('');
  readonly leadSaving = signal(false);
  readonly leadError = signal<string | null>(null);
  readonly leadSent = signal(false);
  private running?: Subscription;

  constructor() { inject(DestroyRef).onDestroy(() => this.running?.unsubscribe()); }

  private initialLang(): Lang {
    const saved = this.read(LANG_KEY);
    if (saved === 'en' || saved === 'ar') return saved;
    return (document.documentElement.lang || navigator.language || '').toLowerCase().startsWith('ar') ? 'ar' : 'en';
  }

  private read(key: string): string | null {
    try { return sessionStorage.getItem(key); } catch { return null; }
  }

  private write(key: string, value: string): void {
    try { sessionStorage.setItem(key, value); } catch { /* private browsing: the chat just will not survive a page change */ }
  }

  private savedMessages(): Message[] {
    try { return JSON.parse(this.read(MESSAGES_KEY) ?? '[]') as Message[]; } catch { return []; }
  }

  show(): void {
    this.open.set(true);
    setTimeout(() => this.boxRef()?.nativeElement.focus());
  }

  toggleLang(): void {
    this.lang.update((l) => (l === 'ar' ? 'en' : 'ar'));
    this.write(LANG_KEY, this.lang());
  }

  money(amount: number, plan: PublicPlan): string { return displayMoney(amount, plan.currency); }

  setLead(key: 'name' | 'email' | 'phone' | 'company' | 'need' | 'consent', value: string | boolean): void {
    this.lead.update((l) => ({ ...l, [key]: value }));
  }

  enter(e: KeyboardEvent): void {
    if (e.shiftKey) return;
    e.preventDefault();
    if (!this.sending()) this.send(this.draft());
  }

  send(raw: string): void {
    const text = raw.trim();
    if (!text || this.sending()) return;
    this.draft.set('');
    this.error.set(null);
    this.sending.set(true);
    this.thinking.set(this.w().thinking);
    this.pushMessage({ role: 'visitor', text });
    this.running = this.api.askBrandAssistant({ token: this.read(TOKEN_KEY), text, website: this.trap() || null }).subscribe({
      next: (e) => {
        switch (e.type) {
          case 'session': this.write(TOKEN_KEY, e.token); break;
          case 'status': if (!this.lastIsReply()) this.thinking.set(e.text === 'Thinking' ? this.w().thinking : e.text); break;
          case 'plans': this.pushMessage({ role: 'assistant', text: '', plans: e.plans, canStart: e.mode !== 'closed' }); break;
          case 'delta': this.thinking.set(null); this.appendReply(e.text); break;
          case 'contact': this.pushMessage({ role: 'assistant', text: '', contact: true }); break;
          case 'error': this.error.set(this.w().failed); this.pushMessage({ role: 'assistant', text: '', contact: true }); break;
        }
      },
      error: (e) => { this.finish(); this.error.set(asProblem(e)?.status === 429 ? this.w().busy : streamErrorMessage(e, this.w().failed)); },
      complete: () => this.finish(),
    });
  }

  stop(): void {
    this.running?.unsubscribe();
    this.finish();
  }

  submitLead(): void {
    const l = this.lead();
    if (this.leadSaving()) return;
    this.leadSaving.set(true);
    this.leadError.set(null);
    this.api.submitBrandLead({
      token: this.read(TOKEN_KEY), name: l.name, email: l.email, phone: l.phone || null, company: l.company || null, need: l.need || null, consent: l.consent, website: this.trap() || null,
    }).subscribe({
      next: () => { this.leadSaving.set(false); this.leadSent.set(true); this.view.set('chat'); },
      error: (e) => { this.leadSaving.set(false); this.leadError.set(problemMessage(e, this.w().failed)); },
    });
  }

  private lastIsReply(): boolean {
    const last = this.messages()[this.messages().length - 1];
    return last?.role === 'assistant' && !!last.text;
  }

  private pushMessage(m: Message): void {
    this.messages.update((list) => [...list, m]);
    this.persist();
  }

  private appendReply(text: string): void {
    this.messages.update((list) => {
      const last = list[list.length - 1];
      return last?.role === 'assistant' && !last.plans && !last.contact ? [...list.slice(0, -1), { ...last, text: last.text + text }] : [...list, { role: 'assistant', text }];
    });
    this.persist();
  }

  private persist(): void {
    this.write(MESSAGES_KEY, JSON.stringify(this.messages()));
    setTimeout(() => { const el = this.logRef()?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }

  private finish(): void {
    this.sending.set(false);
    this.thinking.set(null);
    this.persist();
  }
}
