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
import { AssistantActionCard, streamErrorMessage } from '@core/api/assistant-stream';
import { AssistantConversationRow, WorkspaceAssistantApi } from '@core/api/workspace-assistant.api';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { SessionStore } from '@core/session/session.store';
import { AssistantTextComponent } from '@shared/components/assistant-text/assistant-text.component';

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
  styleUrl: './workspace-assistant.component.scss',
  templateUrl: './workspace-assistant.component.html',
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
