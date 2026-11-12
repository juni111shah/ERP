import { ChangeDetectionStrategy, Component, DestroyRef, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { Subscription } from 'rxjs';
import { streamErrorMessage } from '@core/api/assistant-stream';
import { AssistantBot, PlatformApi } from '@core/api/platform.api';

interface TryMessage { role: 'user' | 'assistant'; text: string; }

/** Lets the founder talk to the connected services and watch a reply arrive, before any assistant is switched on. Nothing here is saved. */
@Component({
  selector: 'erp-assistant-try',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzInputModule, NzRadioModule],
  styleUrl: './assistant-try.component.scss',
  templateUrl: './assistant-try.component.html',
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
