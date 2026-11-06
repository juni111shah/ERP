import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MessageLogApi, MessageLogRow } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { displayDate } from '../../core/format/display';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { countOf } from './finance-kpis';

/** Communication log: every reminder the system sent (in-app, email outbox), with a button to run the reminder pass now. */
@Component({
  selector: 'erp-messages',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, StatusTagComponent, PagedTableComponent, FormsModule, NzButtonModule, NzInputModule, NzTagModule],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search subject or recipient" emptyText="No messages sent yet" [clickable]="false">
      <div toolbar style="display:flex;align-items:center;gap:8px">
        <input aria-label="Test email address" nz-input style="width:200px" placeholder="Test email address" [(ngModel)]="testTo" data-testid="test-email-to" />
        <button nz-button [nzLoading]="testing()" (click)="sendTest()" data-testid="test-email">Send test email</button>
        <button nz-button nzType="primary" [nzLoading]="running()" (click)="run()" data-testid="send-reminders">Send due reminders now</button>
      </div>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'channel') {
          <nz-tag [nzColor]="row.channel === 'email' ? 'blue' : 'purple'">{{ row.channel | erpLabel }}</nz-tag>
        } @else if (column.key === 'subject') {
          <span style="font-weight:600;">{{ row.subject }}</span>
        } @else if (column.key === 'status') {
          <erp-status [value]="row.status" />
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>`,
})
export class MessagesPage {
  private readonly api = inject(MessageLogApi);
  private readonly toast = inject(NzMessageService);
  readonly running = signal(false);
  readonly testing = signal(false);
  testTo = '';
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-sentAt' });
  readonly columns: TableColumn<MessageLogRow>[] = [
    { key: 'sentAt', label: 'Sent', sortField: 'sentAt', width: '170px', value: (m) => displayDate(m.sentAt) },
    { key: 'channel', label: 'Channel', width: '90px' },
    { key: 'subject', label: 'Subject' },
    { key: 'body', label: 'Details' },
    { key: 'status', label: 'Status', width: '100px' },
    { key: 'error', label: 'Error' },
  ];

  sendTest(): void {
    if (!this.testTo.trim()) { this.toast.warning('Enter an email address first'); return; }
    this.testing.set(true);
    this.api.sendTestEmail(this.testTo.trim()).subscribe({
      next: (r) => { this.testing.set(false); if (r.status === 'sent') this.toast.success(`Test email sent to ${this.testTo.trim()}`); else this.toast.error(r.error ?? 'The email could not be sent'); this.list.reload(); },
      error: (e) => { this.testing.set(false); this.toast.error((e?.error?.detail as string | undefined) ?? 'Could not send the test email'); },
    });
  }

  run(): void {
    this.running.set(true);
    this.api.sendReminders().subscribe({
      next: (r: any) => { this.running.set(false); this.toast.success(r.sent ? `${countOf(r.sent, 'reminder')} sent` : 'No reminders are due'); this.list.reload(); },
      error: (e) => { this.running.set(false); this.toast.error((e?.error?.detail as string | undefined) ?? 'Could not send the reminders'); },
    });
  }
}
