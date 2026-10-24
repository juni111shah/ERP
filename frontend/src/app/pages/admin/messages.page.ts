import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MessageLogApi, MessageLogRow } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { I18nService } from '../../core/i18n/i18n.service';

/** Communication log: every reminder the system sent (in-app, email outbox), with a button to run the reminder pass now. */
@Component({
  selector: 'erp-messages',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, FormsModule, NzButtonModule, NzInputModule, NzTagModule],
  providers: [DatePipe],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search subject or recipient" emptyText="No messages sent yet" [clickable]="false">
      <div toolbar style="display:flex;align-items:center;gap:8px">
        <input nz-input style="width:200px" placeholder="Test email address" [(ngModel)]="testTo" data-testid="test-email-to" />
        <button nz-button [nzLoading]="testing()" (click)="sendTest()" data-testid="test-email">Send test email</button>
        <button nz-button nzType="primary" [nzLoading]="running()" (click)="run()" data-testid="send-reminders">Send due reminders now</button>
      </div>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'channel') {
          <nz-tag [nzColor]="row.channel === 'email' ? 'blue' : 'purple'">{{ row.channel }}</nz-tag>
        } @else if (column.key === 'subject') {
          <span style="font-weight:600; color:#1e293b;">{{ row.subject }}</span>
        } @else if (column.key === 'status') {
          <nz-tag [nzColor]="row.status === 'sent' ? 'green' : (row.status === 'failed' ? 'red' : 'default')">{{ row.status }}</nz-tag>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>`,
})
export class MessagesPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(MessageLogApi);
  private readonly date = inject(DatePipe);
  private readonly toast = inject(NzMessageService);
  readonly running = signal(false);
  readonly testing = signal(false);
  testTo = '';
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-sentAt' });
  readonly columns: TableColumn<MessageLogRow>[] = [
    { key: 'sentAt', label: 'Sent', sortField: 'sentAt', width: '170px', value: (m) => this.date.transform(m.sentAt, 'yyyy-MM-dd HH:mm') },
    { key: 'channel', label: 'Channel', width: '90px' },
    { key: 'subject', label: 'Subject' },
    { key: 'body', label: 'Details' },
    { key: 'status', label: 'Status', width: '90px' },
    { key: 'error', label: 'Error' },
  ];

  sendTest(): void {
    if (!this.testTo.trim()) { this.toast.warning('Enter an email address first'); return; }
    this.testing.set(true);
    this.api.sendTestEmail(this.testTo.trim()).subscribe({
      next: (r) => { this.testing.set(false); if (r.status === 'sent') this.toast.success('Test email sent'); else this.toast.error(r.error ?? 'The email could not be sent'); this.list.reload(); },
      error: (e) => { this.testing.set(false); this.toast.error((e?.error?.detail as string | undefined) ?? 'Could not send the test email'); },
    });
  }

  run(): void {
    this.running.set(true);
    this.api.sendReminders().subscribe({
      next: (r: any) => { this.running.set(false); this.toast.success(`${r.sent} ${this.i18n.t('ui.msg.remindersSent', 'reminder(s) sent')}`); this.list.reload(); },
      error: () => this.running.set(false),
    });
  }
}
