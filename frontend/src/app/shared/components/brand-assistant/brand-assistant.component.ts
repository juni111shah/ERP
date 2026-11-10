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
import {
  BRAND_ASSISTANT_KEYS, BRAND_ASSISTANT_WORDS, BrandAssistantLang, BrandAssistantMessage, appendBrandReply, endsWithReply, initialBrandAssistantLang,
  readTabValue, savedConversation, writeTabValue,
} from './brand-assistant-chat';

/** The contact form's fields. */
type LeadField = 'name' | 'email' | 'phone' | 'company' | 'need' | 'consent';

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
  readonly lang = signal<BrandAssistantLang>(initialBrandAssistantLang());
  readonly dir = computed(() => (this.lang() === 'ar' ? 'rtl' : 'ltr'));
  readonly w = computed(() => BRAND_ASSISTANT_WORDS[this.lang()]);
  readonly messages = signal<BrandAssistantMessage[]>(savedConversation());
  readonly draft = signal('');
  readonly sending = signal(false);
  readonly thinking = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly lead = signal({ name: '', email: '', phone: '', company: '', need: '', consent: false });
  /** A field people never see: bots fill it in, and the server then ignores the request. */
  readonly trap = signal('');
  readonly leadSaving = signal(false);
  readonly leadError = signal<string | null>(null);
  readonly leadSent = signal(false);
  private running?: Subscription;

  constructor() { inject(DestroyRef).onDestroy(() => this.running?.unsubscribe()); }

  show(): void {
    this.open.set(true);
    setTimeout(() => this.boxRef()?.nativeElement.focus());
  }

  toggleLang(): void {
    this.lang.update((l) => (l === 'ar' ? 'en' : 'ar'));
    writeTabValue(BRAND_ASSISTANT_KEYS.lang, this.lang());
  }

  money(amount: number, plan: PublicPlan): string { return displayMoney(amount, plan.currency); }

  setLead(key: LeadField, value: string | boolean): void {
    this.lead.update((l) => ({ ...l, [key]: value }));
  }

  /** Enter sends, Shift+Enter starts a new line. */
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
    this.running = this.api.askBrandAssistant({ token: readTabValue(BRAND_ASSISTANT_KEYS.token), text, website: this.trap() || null }).subscribe({
      next: (e) => {
        switch (e.type) {
          case 'session': writeTabValue(BRAND_ASSISTANT_KEYS.token, e.token); break;
          case 'status': if (!endsWithReply(this.messages())) this.thinking.set(e.text === 'Thinking' ? this.w().thinking : e.text); break;
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
      token: readTabValue(BRAND_ASSISTANT_KEYS.token), name: l.name, email: l.email, phone: l.phone || null, company: l.company || null, need: l.need || null, consent: l.consent, website: this.trap() || null,
    }).subscribe({
      next: () => { this.leadSaving.set(false); this.leadSent.set(true); this.view.set('chat'); },
      error: (e) => { this.leadSaving.set(false); this.leadError.set(problemMessage(e, this.w().failed)); },
    });
  }

  private pushMessage(m: BrandAssistantMessage): void {
    this.messages.update((list) => [...list, m]);
    this.persist();
  }

  private appendReply(text: string): void {
    this.messages.update((list) => appendBrandReply(list, text));
    this.persist();
  }

  /** Saves the chat for this tab and scrolls to its end. */
  private persist(): void {
    writeTabValue(BRAND_ASSISTANT_KEYS.messages, JSON.stringify(this.messages()));
    setTimeout(() => { const el = this.logRef()?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }

  private finish(): void {
    this.sending.set(false);
    this.thinking.set(null);
    this.persist();
  }
}
