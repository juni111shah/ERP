import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injectable, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subscription, filter, map, startWith } from 'rxjs';
import { AssistantActionCard, streamErrorMessage } from '../core/api/assistant-stream';
import { AssistantConversationRow, WorkspaceAssistantApi } from '../core/api/workspace-assistant.api';
import { problemMessage } from '../core/http/problem-details';
import { I18nService } from '../core/i18n/i18n.service';
import { SessionStore } from '../core/session/session.store';
import { AssistantTextComponent } from '../shared/assistant-text.component';

const MODULE_KEY = 'assistant';
const PERMISSION = 'assistant.chats.use';
/** A till needs the whole screen, and nobody should chat mid-sale. */
const HIDDEN_ON = ['/pos/terminal'];

/** Whether the assistant is offered here, and whether its panel is open. The top bar's button and the panel share this. */
@Injectable({ providedIn: 'root' })
export class WorkspaceAssistantState {
  private readonly api = inject(WorkspaceAssistantApi);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly url = toSignal(this.router.events.pipe(filter((e) => e instanceof NavigationEnd), map(() => this.router.url), startWith(this.router.url)), { initialValue: this.router.url });

  private readonly serviceReady = signal(false);
  readonly open = signal(false);
  private readonly allowed = computed(() => this.session.isModuleEnabled(MODULE_KEY) && this.session.hasPermission(PERMISSION));
  readonly available = computed(() => this.allowed() && this.serviceReady() && !HIDDEN_ON.some((p) => this.url().startsWith(p)));

  constructor() {
    // Asked only of people who could use it, so nobody else causes a "module disabled" answer. It runs again after a workspace switch.
    effect(() => {
      const me = this.session.me();
      const allowed = this.allowed();
      untracked(() => {
        this.serviceReady.set(false);
        if (!me || !allowed) { this.open.set(false); return; }
        this.api.status().subscribe({ next: (s) => this.serviceReady.set(s.available), error: () => this.serviceReady.set(false) });
      });
    });
  }

  toggle(): void { this.open.update((o) => !o); }
}

type Item =
  | { kind: 'user'; text: string }
  | { kind: 'assistant'; text: string }
  | { kind: 'action'; card: AssistantActionCard; busy?: boolean };

const WORDS = {
  en: {
    title: 'Assistant', close: 'Close the assistant', newChat: 'New chat', history: 'Past chats', back: 'Back to the chat', noHistory: 'No past chats yet.', remove: 'Delete this chat',
    notice: 'AI answers can be wrong, so check anything important. It only uses what you are allowed to see.', hello: 'Ask me about your records, or where to find something.', placeholder: 'Ask a question', send: 'Send', stop: 'Stop', working: 'Working',
    failed: 'Sorry, something went wrong. Please try again.', confirm: 'Confirm', cancel: 'Cancel', open: 'Open', cancelled: 'Cancelled', expired: 'Expired', check: 'Please check this before confirming.',
    suggestions: ['What needs my attention today?', 'Who owes us the most?', 'Where do I add a new customer?'],
  },
  ar: {
    title: 'المساعد', close: 'إغلاق المساعد', newChat: 'محادثة جديدة', history: 'المحادثات السابقة', back: 'العودة إلى المحادثة', noHistory: 'لا توجد محادثات سابقة بعد.', remove: 'حذف هذه المحادثة',
    notice: 'قد تكون إجابات الذكاء الاصطناعي خاطئة، لذا تحقق من أي أمر مهم. لا يستخدم إلا ما يحق لك الاطلاع عليه.', hello: 'اسألني عن سجلاتك أو عن مكان أي شيء في النظام.', placeholder: 'اطرح سؤالاً', send: 'إرسال', stop: 'إيقاف', working: 'جارٍ العمل',
    failed: 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.', confirm: 'تأكيد', cancel: 'إلغاء', open: 'فتح', cancelled: 'أُلغي', expired: 'انتهت صلاحيته', check: 'يرجى التحقق من هذا قبل التأكيد.',
    suggestions: ['ما الذي يحتاج إلى انتباهي اليوم؟', 'من أكثر من يدين لنا بالمال؟', 'أين أضيف عميلاً جديداً؟'],
  },
};

/** The assistant's panel inside a workspace. A change is only ever made by the Confirm button on a card the server built. */
@Component({
  selector: 'erp-workspace-assistant',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, RouterLink, NzButtonModule, NzIconModule, NzInputModule, AssistantTextComponent],
  styles: [`
    .panel { position: fixed; inset-block: 0; inset-inline-end: 0; z-index: 900; width: 420px; max-width: 100vw; display: flex; flex-direction: column; background: var(--erp-color-bg-surface, #fff); color: var(--erp-color-text-primary, #0f172a); border-inline-start: 1px solid var(--erp-color-border-subtle, #e2e8f0); box-shadow: var(--erp-shadow-3, 0 8px 24px rgba(0,0,0,.16)); font-size: 14px; }
    .head { display: flex; align-items: center; gap: 6px; padding: 12px 14px; border-bottom: 1px solid var(--erp-color-border-subtle, #e2e8f0); }
    .head strong { flex: 1; min-width: 0; }
    .notice { padding: 8px 14px; font-size: 12px; color: var(--erp-color-text-muted, #64748b); background: var(--erp-color-bg-subtle, #f8fafc); }
    .log { flex: 1; min-height: 0; overflow-y: auto; padding: 12px 14px; display: flex; flex-direction: column; gap: 8px; }
    .bubble { max-width: 90%; padding: 8px 12px; border-radius: 12px; }
    .bubble.user { align-self: flex-end; background: var(--erp-color-primary-subtle, #e7f6ef); white-space: pre-wrap; overflow-wrap: anywhere; }
    .bubble.assistant { align-self: flex-start; background: var(--erp-color-bg-subtle, #f1f5f9); }
    .card { border: 1px solid var(--erp-color-primary-border, #10955d); border-radius: 10px; padding: 10px 12px; }
    .card strong { display: block; margin-bottom: 6px; }
    .card dl { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 2px 12px; margin: 0 0 8px; }
    .card dt { color: var(--erp-color-text-muted, #64748b); }
    .card dd { margin: 0; overflow-wrap: anywhere; }
    .card .buttons { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
    .card .hint { color: var(--erp-color-text-muted, #64748b); font-size: 12.5px; margin-bottom: 8px; }
    .card .outcome { font-size: 13px; }
    .card .outcome.failed { color: var(--erp-color-error-text, #b91c1c); }
    .status { color: var(--erp-color-text-muted, #64748b); font-size: 12.5px; }
    .error { color: var(--erp-color-error-text, #b91c1c); font-size: 13px; }
    .chips { display: flex; flex-direction: column; align-items: flex-start; gap: 6px; }
    .chips button { border: 1px solid var(--erp-color-border-subtle, #e2e8f0); background: none; border-radius: 16px; padding: 5px 12px; cursor: pointer; color: inherit; font: inherit; text-align: start; }
    .chips button:focus-visible { outline: 2px solid var(--erp-color-focus-ring, #10955d); outline-offset: 2px; }
    .send { display: flex; gap: 8px; align-items: flex-end; padding: 10px 14px; border-top: 1px solid var(--erp-color-border-subtle, #e2e8f0); }
    .send textarea { flex: 1; min-width: 0; resize: none; }
    .past { flex: 1; min-height: 0; overflow-y: auto; padding: 8px 14px; }
    .past-row { display: flex; align-items: center; gap: 6px; padding: 6px 0; border-bottom: 1px solid var(--erp-color-border-subtle, #e2e8f0); }
    .past-row button.title { flex: 1; min-width: 0; text-align: start; background: none; border: 0; cursor: pointer; color: inherit; font: inherit; padding: 2px 0; overflow: hidden; }
    .past-row small { display: block; color: var(--erp-color-text-muted, #64748b); }
    @media (max-width: 767px) { .panel { width: 100%; border: 0; } }
  `],
  template: `
    @if (state.available() && state.open()) {
      <!-- data-no-i18n: the page translator must never rewrite what the person or the model wrote. -->
      <section class="panel" role="complementary" [attr.aria-label]="w().title" [attr.dir]="i18n.dir()" data-no-i18n (keydown.escape)="state.open.set(false)" data-testid="workspace-assistant">
        <div class="head">
          <img src="highersquare-assistant.svg" alt="" width="28" height="28" />
          <strong>{{ w().title }}</strong>
          <button nz-button nzType="text" nzSize="small" type="button" [attr.aria-label]="w().newChat" (click)="newChat()" data-testid="workspace-assistant-new"><span nz-icon nzType="plus"></span></button>
          <button nz-button nzType="text" nzSize="small" type="button" [attr.aria-label]="w().history" (click)="toggleHistory()" data-testid="workspace-assistant-history"><span nz-icon nzType="history"></span></button>
          <button nz-button nzType="text" nzSize="small" type="button" [attr.aria-label]="w().close" (click)="state.open.set(false)" data-testid="workspace-assistant-close"><span nz-icon nzType="close"></span></button>
        </div>
        <div class="notice">{{ w().notice }}</div>

        @if (showHistory()) {
          <div class="past" data-testid="workspace-assistant-past">
            @for (c of past(); track c.id) {
              <div class="past-row">
                <button type="button" class="title" dir="auto" (click)="openConversation(c.id)">{{ c.title }}<small>{{ c.lastMessageAt | date: 'medium' }}</small></button>
                <button nz-button nzType="text" nzSize="small" nzDanger type="button" [attr.aria-label]="w().remove" (click)="remove(c.id)"><span nz-icon nzType="delete"></span></button>
              </div>
            } @empty { <p class="status">{{ w().noHistory }}</p> }
          </div>
        } @else {
          <div class="log" #log aria-live="polite" data-testid="workspace-assistant-log">
            @if (!items().length) {
              <div class="bubble assistant">{{ w().hello }}</div>
              <div class="chips">@for (s of w().suggestions; track s) { <button type="button" (click)="send(s)" data-testid="workspace-assistant-suggestion">{{ s }}</button> }</div>
            }
            @for (item of items(); track $index) {
              @switch (item.kind) {
                @case ('user') { <div class="bubble user" dir="auto" data-testid="workspace-assistant-user">{{ item.text }}</div> }
                @case ('assistant') { <div class="bubble assistant" data-testid="workspace-assistant-reply"><erp-assistant-text [text]="item.text" /></div> }
                @case ('action') {
                  <div class="card" [attr.data-testid]="'workspace-assistant-card-' + item.card.status">
                    <strong dir="auto">{{ item.card.title }}</strong>
                    <dl dir="auto">@for (l of item.card.lines; track $index) { <dt>{{ l.label }}</dt><dd>{{ l.value }}</dd> }</dl>
                    @if (item.card.status === 'pending') {
                      <div class="hint">{{ w().check }}</div>
                      <div class="buttons">
                        <button nz-button nzType="primary" nzSize="small" type="button" [nzLoading]="item.busy" (click)="confirm(item.card)" data-testid="workspace-assistant-confirm">{{ w().confirm }}</button>
                        <button nz-button nzSize="small" type="button" [disabled]="item.busy" (click)="cancel(item.card)" data-testid="workspace-assistant-cancel">{{ w().cancel }}</button>
                      </div>
                    } @else if (item.card.status === 'cancelled') {
                      <div class="outcome">{{ w().cancelled }}</div>
                    } @else {
                      <div class="outcome" [class.failed]="item.card.status !== 'done'" dir="auto" data-testid="workspace-assistant-outcome">{{ item.card.message || w().expired }}</div>
                      @if (item.card.link) { <a [routerLink]="item.card.link">{{ w().open }}</a> }
                    }
                  </div>
                }
              }
            }
            @if (working()) { <div class="status" data-testid="workspace-assistant-status">{{ working() }}...</div> }
            @if (error(); as e) { <div class="error" role="alert" data-testid="workspace-assistant-error">{{ e }}</div> }
          </div>
          <div class="send">
            <textarea #box nz-input rows="2" maxlength="2000" dir="auto" [attr.aria-label]="w().placeholder" [placeholder]="w().placeholder" [ngModel]="draft()" (ngModelChange)="draft.set($event)" (keydown.enter)="enter($any($event))" data-testid="workspace-assistant-input"></textarea>
            @if (sending()) {
              <button nz-button type="button" (click)="stop()" data-testid="workspace-assistant-stop">{{ w().stop }}</button>
            } @else {
              <button nz-button nzType="primary" type="button" [disabled]="!draft().trim()" (click)="send(draft())" data-testid="workspace-assistant-send">{{ w().send }}</button>
            }
          </div>
        }
      </section>
    }`,
})
export class WorkspaceAssistantComponent {
  private readonly api = inject(WorkspaceAssistantApi);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  private readonly logRef = viewChild<ElementRef<HTMLElement>>('log');
  private readonly boxRef = viewChild<ElementRef<HTMLTextAreaElement>>('box');

  readonly state = inject(WorkspaceAssistantState);
  readonly i18n = inject(I18nService);
  readonly w = computed(() => WORDS[this.i18n.lang()]);
  readonly items = signal<Item[]>([]);
  readonly draft = signal('');
  readonly sending = signal(false);
  readonly working = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly showHistory = signal(false);
  readonly past = signal<AssistantConversationRow[]>([]);
  private conversationId: string | null = null;
  private running?: Subscription;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.running?.unsubscribe());
    // Another workspace must never see this one's open chat.
    effect(() => {
      this.session.me()?.workspace.id;
      untracked(() => this.newChat());
    });
    effect(() => { if (this.state.open()) setTimeout(() => this.boxRef()?.nativeElement.focus()); });
  }

  enter(e: KeyboardEvent): void {
    if (e.shiftKey) return;
    e.preventDefault();
    if (!this.sending()) this.send(this.draft());
  }

  newChat(): void {
    this.running?.unsubscribe();
    this.sending.set(false);
    this.working.set(null);
    this.conversationId = null;
    this.items.set([]);
    this.error.set(null);
    this.showHistory.set(false);
  }

  toggleHistory(): void {
    if (this.showHistory()) { this.showHistory.set(false); return; }
    this.showHistory.set(true);
    this.api.conversations().subscribe({ next: (list) => this.past.set(list), error: (e) => this.message.error(problemMessage(e, this.w().failed)) });
  }

  openConversation(id: string): void {
    this.api.conversation(id).subscribe({
      next: (c) => {
        const timed: { at: string; item: Item }[] = [
          ...c.messages.map((m) => ({ at: m.at, item: (m.fromUser ? { kind: 'user', text: m.text } : { kind: 'assistant', text: m.text }) as Item })),
          ...c.actions.map((a) => ({ at: a.createdAt, item: { kind: 'action', card: a } as Item })),
        ];
        this.items.set(timed.sort((x, y) => x.at.localeCompare(y.at)).map((t) => t.item));
        this.conversationId = c.id;
        this.error.set(null);
        this.showHistory.set(false);
        this.scroll();
      },
      error: (e) => this.message.error(problemMessage(e, this.w().failed)),
    });
  }

  remove(id: string): void {
    this.api.deleteConversation(id).subscribe({
      next: () => {
        this.past.update((list) => list.filter((c) => c.id !== id));
        if (this.conversationId === id) { this.conversationId = null; this.items.set([]); }
      },
      error: (e) => this.message.error(problemMessage(e, this.w().failed)),
    });
  }

  send(raw: string): void {
    const text = raw.trim();
    if (!text || this.sending()) return;
    this.draft.set('');
    this.error.set(null);
    this.sending.set(true);
    this.working.set(this.w().working);
    this.items.update((list) => [...list, { kind: 'user', text }]);
    this.scroll();
    this.running = this.api.ask({ conversationId: this.conversationId, text, page: this.router.url }).subscribe({
      next: (e) => {
        switch (e.type) {
          case 'conversation': this.conversationId = e.id; break;
          case 'status': this.working.set(this.i18n.lang() === 'ar' ? this.w().working : e.text); break;
          case 'delta': this.working.set(null); this.append(e.text); break;
          case 'action': this.items.update((list) => [...list, { kind: 'action', card: e.action }]); this.scroll(); break;
          case 'error': this.error.set(e.message); break;
        }
      },
      error: (e) => { this.finish(); this.error.set(streamErrorMessage(e, this.w().failed)); },
      complete: () => this.finish(),
    });
  }

  stop(): void {
    this.running?.unsubscribe();
    this.finish();
  }

  confirm(card: AssistantActionCard): void {
    this.setBusy(card.id, true);
    this.api.confirm(card.id).subscribe({
      next: (updated) => this.replaceCard(updated),
      error: (e) => { this.setBusy(card.id, false); this.message.error(problemMessage(e, this.w().failed)); },
    });
  }

  cancel(card: AssistantActionCard): void {
    this.setBusy(card.id, true);
    this.api.cancel(card.id).subscribe({
      next: (updated) => this.replaceCard(updated),
      error: (e) => { this.setBusy(card.id, false); this.message.error(problemMessage(e, this.w().failed)); },
    });
  }

  private setBusy(id: string, busy: boolean): void {
    this.items.update((list) => list.map((i) => (i.kind === 'action' && i.card.id === id ? { ...i, busy } : i)));
  }

  private replaceCard(card: AssistantActionCard): void {
    this.items.update((list) => list.map((i) => (i.kind === 'action' && i.card.id === card.id ? { kind: 'action', card } : i)));
    this.scroll();
  }

  private append(text: string): void {
    this.items.update((list) => {
      const last = list[list.length - 1];
      return last?.kind === 'assistant' ? [...list.slice(0, -1), { ...last, text: last.text + text }] : [...list, { kind: 'assistant', text }];
    });
    this.scroll();
  }

  private scroll(): void {
    setTimeout(() => { const el = this.logRef()?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }

  private finish(): void {
    this.sending.set(false);
    this.working.set(null);
  }
}
