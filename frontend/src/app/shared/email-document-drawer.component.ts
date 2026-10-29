import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../core/api/api-config';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { problemMessage } from '../core/http/problem-details';

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
  template: `
    <nz-drawer [nzVisible]="open()" [nzTitle]="title() || ('email.title' | translate: 'Send by email')" nzWidth="440" (nzOnClose)="closed.emit()" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <label for="email-to">{{ 'email.to' | translate: 'To' }}</label>
        <input nz-input id="email-to" type="email" [(ngModel)]="to" autocomplete="off" dir="ltr" data-testid="email-to" />
        <small style="opacity:.65">{{ 'email.toHint' | translate: "Leave empty to use the party's email address." }}</small>
        <label for="email-message" style="display:block;margin-top:16px">{{ 'email.message' | translate: 'Message (optional)' }}</label>
        <textarea nz-input id="email-message" rows="5" maxlength="1000" [(ngModel)]="message" data-testid="email-message"></textarea>
        @if (error()) { <p style="color:#cf1322;margin-top:12px" data-testid="email-error">{{ error() }}</p> }
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button type="button" (click)="closed.emit()">{{ 'common.cancel' | translate: 'Cancel' }}</button>
      <button nz-button nzType="primary" type="button" [nzLoading]="busy()" (click)="send()" style="margin-inline-start:8px" data-testid="email-send">{{ 'email.send' | translate: 'Send' }}</button>
    </ng-template>`,
})
export class EmailDocumentDrawerComponent {
  readonly open = input.required<boolean>();
  readonly path = input.required<string>();
  readonly title = input<string>('');
  readonly params = input<Record<string, string>>({});
  readonly closed = output<void>();
  readonly sent = output<string>();

  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly toast = inject(NzMessageService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  to = '';
  message = '';

  constructor() { effect(() => { if (this.open()) untracked(() => { this.to = ''; this.message = ''; this.error.set(null); }); }); }

  send(): void {
    this.busy.set(true);
    this.error.set(null);
    this.http.post<{ to: string }>(`${this.base}/${this.path()}`, { to: this.to.trim() || null, message: this.message.trim() || null }, { params: this.params() }).subscribe({
      next: (r) => { this.busy.set(false); this.toast.success(`Email sent to ${r.to}`); this.sent.emit(r.to); this.closed.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(problemMessage(e, 'Could not send the email', 'to')); },
    });
  }
}
