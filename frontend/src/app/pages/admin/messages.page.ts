import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MessageLogApi, MessageLogRow } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

/** Communication log: every reminder the system sent (in-app, email outbox), with a button to run the reminder pass now. */
@Component({
  selector: 'erp-messages',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, NzButtonModule],
  providers: [DatePipe],
  template: `
    <div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
      <h1 class="erp-page-title" style="margin:0">Message log</h1>
      <span style="flex:1"></span>
      <button nz-button nzType="primary" [nzLoading]="running()" (click)="run()" data-testid="send-reminders">Send due reminders now</button>
    </div>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search subject or recipient" emptyText="No messages sent yet" [clickable]="false" />`,
})
export class MessagesPage {
  private readonly api = inject(MessageLogApi);
  private readonly date = inject(DatePipe);
  private readonly toast = inject(NzMessageService);
  readonly running = signal(false);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-sentAt' });
  readonly columns: TableColumn<MessageLogRow>[] = [
    { key: 'sentAt', label: 'Sent', sortField: 'sentAt', width: '170px', value: (m) => this.date.transform(m.sentAt, 'yyyy-MM-dd HH:mm') },
    { key: 'channel', label: 'Channel', width: '90px' },
    { key: 'subject', label: 'Subject' },
    { key: 'body', label: 'Details' },
    { key: 'status', label: 'Status', width: '90px' },
  ];

  run(): void {
    this.running.set(true);
    this.api.sendReminders().subscribe({
      next: (r) => { this.running.set(false); this.toast.success(`${r.sent} reminder(s) sent`); this.list.reload(); },
      error: () => this.running.set(false),
    });
  }
}
