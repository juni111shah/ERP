import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { DocumentEmailApi } from '@core/api/document-email.api';
import { problemMessage } from '@core/http/problem-details';
import { phraseNow, tNow } from '@core/i18n/translate-now';
import { TranslatePipe } from '@core/i18n/translate.pipe';

/** "Invoice INV-1" with its first word in the current language. */
function phraseFirstWord(label: string): string {
  const [word, ...rest] = label.split(' ');
  return [phraseNow(word), ...rest].join(' ');
}

/**
 * Send a document or statement by email: recipient (empty = the party's own address) and an optional message.
 *   <erp-email-document-drawer [open]="emailOpen()" [path]="'finance/invoices/' + id + '/email'" (closed)="emailOpen.set(false)" />
 * `path` is relative to the API base; `params` become the query string (for example the statement date).
 */
@Component({
  selector: 'erp-email-document-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzDrawerModule, NzInputModule, TranslatePipe],
  templateUrl: './email-document-drawer.component.html',
})
export class EmailDocumentDrawerComponent {
  readonly open = input.required<boolean>();
  readonly path = input.required<string>();
  readonly title = input<string>('');
  /** What is being sent, so the toast names it: "Invoice INV-1". */
  readonly documentLabel = input<string>('');
  readonly params = input<Record<string, string>>({});
  readonly closed = output<void>();
  readonly sent = output<string>();

  private readonly emailApi = inject(DocumentEmailApi);
  private readonly toast = inject(NzMessageService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  to = '';
  message = '';

  constructor() { effect(() => { if (this.open()) untracked(() => { this.to = ''; this.message = ''; this.error.set(null); }); }); }

  send(): void {
    this.busy.set(true);
    this.error.set(null);
    this.emailApi.send(this.path(), { to: this.to.trim() || null, message: this.message.trim() || null }, this.params()).subscribe({
      next: (r) => { this.busy.set(false); this.toast.success(this.sentText(r.to)); this.sent.emit(r.to); this.closed.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(problemMessage(e, 'Could not send the email', 'to')); },
    });
  }

  /** "Invoice INV-1 sent to a@b.c", in the current language. */
  private sentText(to: string): string {
    const what = this.documentLabel() ? phraseFirstWord(this.documentLabel()) : phraseNow('Email');
    return tNow('ui.success.sentTo', '{what} sent to {to}', { what, to });
  }
}
