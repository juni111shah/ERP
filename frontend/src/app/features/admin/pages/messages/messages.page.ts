import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { MessageLogApi, MessageLogRow } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { displayDate } from '@core/format/display';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { countOf } from '@shared/utils/kpi-stats';

/** The server's own explanation of a failed call, when it sent one. */
const detailOf = (e: { error?: { detail?: unknown } } | null | undefined): string | undefined => e?.error?.detail as string | undefined;

/** Communication log: every reminder the system sent (in-app, email outbox), with a button to run the reminder pass now. */
@Component({
  selector: 'erp-messages',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, StatusTagComponent, PagedTableComponent, HasPermissionDirective, FormsModule, NzButtonModule, NzInputModule, NzTagModule],
  templateUrl: './messages.page.html',
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
      error: (e) => { this.testing.set(false); this.toast.error(detailOf(e) ?? 'Could not send the test email'); },
    });
  }

  run(): void {
    this.running.set(true);
    this.api.sendReminders().subscribe({
      next: (r) => { this.running.set(false); this.toast.success(r.sent ? `${countOf(r.sent, 'reminder')} sent` : 'No reminders are due'); this.list.reload(); },
      error: (e) => { this.running.set(false); this.toast.error(detailOf(e) ?? 'Could not send the reminders'); },
    });
  }
}
