import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { Subscription } from 'rxjs';
import { streamErrorMessage } from '../../core/api/assistant-stream';
import { AssistantBot, PlatformApi } from '../../core/api/platform.api';

interface TryMessage { role: 'user' | 'assistant'; text: string; }

/** Lets the founder talk to the connected services and watch a reply arrive, before any assistant is switched on. Nothing here is saved. */
@Component({
  selector: 'erp-assistant-try',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzInputModule, NzRadioModule],
  styles: [`
    :host { display: block; }
    .which { margin-bottom: 12px; }
    .log { display: flex; flex-direction: column; gap: 8px; max-height: 320px; overflow-y: auto; margin-bottom: 12px; }
    .bubble { max-width: 85%; padding: 8px 12px; border-radius: 10px; white-space: pre-wrap; overflow-wrap: anywhere; line-height: 1.5; }
    .bubble.user { align-self: flex-end; background: var(--erp-color-primary-subtle); color: var(--erp-color-text-primary); }
    .bubble.assistant { align-self: flex-start; background: var(--erp-color-bg-subtle); color: var(--erp-color-text-primary); }
    .status { color: var(--erp-color-text-muted); font-size: 12.5px; }
    .error { color: var(--erp-color-error-text); font-size: 13px; margin-bottom: 8px; }
    .send { display: flex; gap: 8px; align-items: flex-end; }
    .send textarea { flex: 1; min-width: 0; }
    .meta { color: var(--erp-color-text-muted); font-size: 12.5px; margin-top: 6px; }
  `],
  template: `
    <div class="which">
      <nz-radio-group [ngModel]="bot()" (ngModelChange)="bot.set($event)" nzButtonStyle="solid" [nzDisabled]="sending()" data-testid="assistant-try-bot">
        <label nz-radio-button nzValue="brand">Public assistant's services</label>
        <label nz-radio-button nzValue="workspace">Client data services</label>
      </nz-radio-group>
    </div>
    @if (messages().length) {
      <!-- data-no-i18n: the page translator must never rewrite what the person or the model wrote. -->
      <div class="log" data-no-i18n aria-live="polite" data-testid="assistant-try-log">
        @for (m of messages(); track $index) {
          <div class="bubble" [class.user]="m.role === 'user'" [class.assistant]="m.role === 'assistant'" dir="auto" [attr.data-testid]="'assistant-try-' + m.role">{{ m.text }}</div>
        }
        @if (status(); as s) { <div class="status" data-testid="assistant-try-status">{{ s }}...</div> }
      </div>
    }
    @if (error(); as e) { <div class="error" role="alert" data-testid="assistant-try-error">{{ e }}</div> }
    <div class="send">
      <textarea nz-input rows="2" maxlength="2000" dir="auto" aria-label="Your message" placeholder="Type a message" [ngModel]="draft()" (ngModelChange)="draft.set($event)" (keydown.enter)="enter($any($event))" data-testid="assistant-try-input"></textarea>
      @if (sending()) {
        <button nz-button type="button" (click)="stop()" data-testid="assistant-try-stop">Stop</button>
      } @else {
        <button nz-button nzType="primary" type="button" [disabled]="!draft().trim()" (click)="send()" data-testid="assistant-try-send">Send</button>
      }
      @if (messages().length && !sending()) { <button nz-button type="button" (click)="clear()" data-testid="assistant-try-clear">Clear</button> }
    </div>
    @if (meta(); as m) { <div class="meta" data-testid="assistant-try-meta">{{ m }}</div> }`,
})
export class AssistantTryComponent {
  private readonly api = inject(PlatformApi);

  /** A reply finished or failed, so usage on the page has changed. */
  readonly used = output<void>();

  readonly bot = signal<AssistantBot>('brand');
  readonly messages = signal<TryMessage[]>([]);
  readonly draft = signal('');
  readonly sending = signal(false);
  readonly status = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly meta = signal<string | null>(null);
  private running?: Subscription;

  constructor() { inject(DestroyRef).onDestroy(() => this.running?.unsubscribe()); }

  enter(e: KeyboardEvent): void {
    if (e.shiftKey) return;
    e.preventDefault();
    if (!this.sending()) this.send();
  }

  send(): void {
    const text = this.draft().trim();
    if (!text || this.sending()) return;
    this.draft.set('');
    this.error.set(null);
    this.meta.set(null);
    const history: TryMessage[] = [...this.messages(), { role: 'user', text }];
    this.messages.set(history);
    this.sending.set(true);
    this.status.set('Thinking');
    let service = '';
    // Sending the last messages only keeps a long try-out inside the server's limit.
    this.running = this.api.tryAssistant({ bot: this.bot(), messages: history.slice(-20) }).subscribe({
      next: (e) => {
        switch (e.type) {
          case 'status': this.status.set(e.text); break;
          case 'service': service = e.name; break;
          case 'delta': this.status.set(null); this.append(e.text); break;
          case 'done': this.meta.set(`Answered by ${service || 'the service'} · ${(e.inputTokens + e.outputTokens).toLocaleString()} tokens`); break;
          case 'error': this.error.set(e.message); break;
        }
      },
      error: (e) => { this.finish(); this.error.set(streamErrorMessage(e, 'Could not send the message.')); },
      complete: () => this.finish(),
    });
  }

  stop(): void {
    this.running?.unsubscribe();
    this.finish();
    // The server notices the stop a moment later, and only then counts what was used.
    setTimeout(() => this.used.emit(), 1000);
  }

  clear(): void {
    this.messages.set([]);
    this.error.set(null);
    this.meta.set(null);
  }

  private append(text: string): void {
    this.messages.update((list) => {
      const last = list[list.length - 1];
      return last?.role === 'assistant' ? [...list.slice(0, -1), { ...last, text: last.text + text }] : [...list, { role: 'assistant', text }];
    });
  }

  private finish(): void {
    this.sending.set(false);
    this.status.set(null);
    // A question that got no answer goes back in the box, ready to send again.
    const list = this.messages();
    const last = list[list.length - 1];
    if (last?.role === 'user') {
      this.messages.set(list.slice(0, -1));
      if (!this.draft()) this.draft.set(last.text);
    }
    this.used.emit();
  }
}
