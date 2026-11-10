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
  styleUrl: './brand-assistant.component.scss',
  templateUrl: './brand-assistant.component.html',
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
