import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AttachmentItem, AttachmentsApi } from '@core/api/attachments.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { holdsPermission } from '../../directives/has-permission.directive';
import { ErpDatePipe, ErpLabelPipe } from '../../pipes/display.pipes';
import { saveBlob } from '../../utils/save-blob';
import { RowActionsComponent } from '../row-actions/row-actions.component';
import { TableToolsComponent, matchText } from '../table-tools/table-tools.component';
import { attachmentUploadForm, expiryTone } from './attachment-expiry';

export type { AttachmentItem } from '@core/api/attachments.api';
export { daysUntil, expiryTone } from './attachment-expiry';

/**
 * Documents of one record: list, upload (with category and optional expiry), download and delete. <erp-attachments ownerType="lease" [ownerId]="id" />
 * Listing needs `core.attachments.read`; upload and delete need `core.attachments.write` and the owner record's own write permission,
 * which the host passes as `writePermission` (the server checks both).
 */
@Component({
  selector: 'erp-attachments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, ErpLabelPipe, ErpDatePipe, TableToolsComponent, DatePipe, FormsModule, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzTableModule, NzTagModule],
  templateUrl: './attachments.component.html',
})
export class AttachmentsComponent {
  readonly ownerType = input.required<string>();
  readonly ownerId = input.required<string>();
  /** The owner record's write permission (for example 'realestate.leases.write'), needed with core.attachments.write to upload or delete. */
  readonly writePermission = input<string | readonly string[] | null>(null);
  private readonly attachmentsApi = inject(AttachmentsApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly canRead = computed(() => this.session.hasPermission('core.attachments.read'));
  readonly canWrite = computed(() => this.session.hasPermission('core.attachments.write') && holdsPermission(this.session, this.writePermission()));
  readonly items = signal<AttachmentItem[]>([]);
  readonly q = signal('');
  /** Client-side filter: GET /attachments only takes the owner. */
  readonly shown = computed(() => this.items().filter((a) => matchText(this.q(), a.fileName, a.category)));
  readonly busy = signal(false);
  category = '';
  expires: Date | null = null;

  constructor() { effect(() => { this.ownerType(); this.ownerId(); if (this.canRead()) untracked(() => this.load()); }); }

  tone(date: string): string { return expiryTone(date); }

  load(): void {
    this.attachmentsApi.list(this.ownerType(), this.ownerId()).subscribe((r) => this.items.set(r));
  }

  upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) { this.message.warning('Choose a file first'); return; }
    const form = attachmentUploadForm(this.ownerType(), this.ownerId(), file, this.category, this.expires);
    this.busy.set(true);
    this.attachmentsApi.upload(form).subscribe({
      next: () => { this.busy.set(false); input.value = ''; this.category = ''; this.expires = null; this.message.success(`Document ${file.name} uploaded`); this.load(); },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Upload failed'); },
    });
  }

  download(a: { id: string; fileName: string }): void {
    this.attachmentsApi.download(a.id).subscribe((blob) => saveBlob(blob, a.fileName));
  }

  remove(a: AttachmentItem): void {
    this.attachmentsApi.delete(a.id).subscribe(() => { this.message.success(doneText('removed', 'Document', a.fileName)); this.load(); });
  }
}

/**
 * Fetches a file with the auth header and hands it to the browser as a download. Kept for callers that hold an HttpClient and API base;
 * inside components prefer `AttachmentsApi.download` with `saveBlob`.
 */
export function downloadAttachment(http: HttpClient, base: string, a: { id: string; fileName: string }): void {
  http.get(`${base}/attachments/${a.id}/download`, { responseType: 'blob' }).subscribe((blob) => saveBlob(blob, a.fileName));
}
