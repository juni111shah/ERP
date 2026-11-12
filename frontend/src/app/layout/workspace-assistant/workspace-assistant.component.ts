import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subscription } from 'rxjs';
import { AssistantActionCard, streamErrorMessage } from '@core/api/assistant-stream';
import { AssistantConversationRow, WorkspaceAssistantApi } from '@core/api/workspace-assistant.api';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { SessionStore } from '@core/session/session.store';
import { AssistantTextComponent } from '@shared/components/assistant-text/assistant-text.component';
import { ASSISTANT_WORDS, AssistantChatItem, appendReply, conversationItems, withCard, withCardBusy } from './workspace-assistant-chat';
import { WorkspaceAssistantState } from './workspace-assistant-state';

export { WorkspaceAssistantState } from './workspace-assistant-state';

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
  readonly w = computed(() => ASSISTANT_WORDS[this.i18n.lang()]);
  readonly items = signal<AssistantChatItem[]>([]);
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

  /** Enter sends, Shift+Enter starts a new line. */
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
    this.api.conversations().subscribe({ next: (list) => this.past.set(list), error: (e) => this.showError(e) });
  }

  openConversation(id: string): void {
    this.api.conversation(id).subscribe({
      next: (c) => {
        this.items.set(conversationItems(c));
        this.conversationId = c.id;
        this.error.set(null);
        this.showHistory.set(false);
        this.scroll();
      },
      error: (e) => this.showError(e),
    });
  }

  remove(id: string): void {
    this.api.deleteConversation(id).subscribe({
      next: () => {
        this.past.update((list) => list.filter((c) => c.id !== id));
        if (this.conversationId === id) { this.conversationId = null; this.items.set([]); }
      },
      error: (e) => this.showError(e),
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
          case 'delta': this.working.set(null); this.items.update((list) => appendReply(list, e.text)); this.scroll(); break;
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
      error: (e) => { this.setBusy(card.id, false); this.showError(e); },
    });
  }

  cancel(card: AssistantActionCard): void {
    this.setBusy(card.id, true);
    this.api.cancel(card.id).subscribe({
      next: (updated) => this.replaceCard(updated),
      error: (e) => { this.setBusy(card.id, false); this.showError(e); },
    });
  }

  private setBusy(id: string, busy: boolean): void {
    this.items.update((list) => withCardBusy(list, id, busy));
  }

  private replaceCard(card: AssistantActionCard): void {
    this.items.update((list) => withCard(list, card));
    this.scroll();
  }

  private showError(e: unknown): void {
    this.message.error(problemMessage(e, this.w().failed));
  }

  private scroll(): void {
    setTimeout(() => { const el = this.logRef()?.nativeElement; if (el) el.scrollTop = el.scrollHeight; });
  }

  private finish(): void {
    this.sending.set(false);
    this.working.set(null);
  }
}
