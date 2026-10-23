import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { toIsoDate } from '../core/custom-fields/custom-field-form';
import { asProblem } from '../core/http/problem-details';
import { HasPermissionDirective } from './has-permission.directive';
import { matchText, TableToolsComponent } from './table-tools.component';

export interface AttachmentItem { id: string; fileName: string; contentType: string; sizeBytes: number; createdAt: string; category?: string; description?: string; expiresOn?: string; }

/** Days until a date (negative = past); null when there is no date. */
export function daysUntil(date?: string | null): number | null {
  if (!date) return null;
  const [y, m, d] = date.split('-').map(Number);
  return Math.round((new Date(y, m - 1, d).getTime() - new Date(new Date().getFullYear(), new Date().getMonth(), new Date().getDate()).getTime()) / 86400000);
}

/** Documents of one record: list, upload (with category and optional expiry), download and delete. <erp-attachments ownerType="lease" [ownerId]="id" /> */
@Component({
  selector: 'erp-attachments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TableToolsComponent, DatePipe, FormsModule, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzTableModule, NzTagModule],
  template: `
    <h3>Documents</h3>
    <erp-table-tools [(search)]="q" (refresh)="load()" testId="attachments"></erp-table-tools>
    <nz-table [nzData]="shown()" nzSize="small" [nzShowPagination]="false" data-testid="attachments">
      <thead><tr><th>File</th><th>Category</th><th>Expires</th><th>Added</th><th></th></tr></thead>
      <tbody>
        @for (a of shown(); track a.id) {
          <tr>
            <td><a (click)="download(a)" style="cursor:pointer">{{ a.fileName }}</a></td>
            <td>{{ a.category }}</td>
            <td>@if (a.expiresOn) { <nz-tag [nzColor]="tone(a.expiresOn)">{{ a.expiresOn }}</nz-tag> }</td>
            <td>{{ a.createdAt | date: 'yyyy-MM-dd' }}</td>
            <td><button *erpHasPermission="'core.attachments.write'" nz-button nzType="text" nzSize="small" (click)="remove(a)"><span nz-icon nzType="delete"></span></button></td>
          </tr>
        } @empty { <tr><td colspan="5">No documents yet.</td></tr> }
      </tbody>
    </nz-table>
    <div *erpHasPermission="'core.attachments.write'" style="display:flex;gap:8px;flex-wrap:wrap;margin-top:8px;align-items:center">
      <input type="file" #file data-testid="attachment-file" />
      <input nz-input style="width:180px" placeholder="Category (e.g. Ejari)" [(ngModel)]="category" />
      <nz-date-picker [(ngModel)]="expires" nzPlaceHolder="Expires on (optional)"></nz-date-picker>
      <button nz-button nzType="primary" [nzLoading]="busy()" (click)="upload(file)" data-testid="attachment-upload">Upload</button>
    </div>`,
})
export class AttachmentsComponent {
  readonly ownerType = input.required<string>();
  readonly ownerId = input.required<string>();
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  readonly items = signal<AttachmentItem[]>([]);
  readonly q = signal('');
  /** Client-side filter: GET /attachments only takes the owner. */
  readonly shown = computed(() => this.items().filter((a) => matchText(this.q(), a.fileName, a.category)));
  readonly busy = signal(false);
  category = '';
  expires: Date | null = null;

  constructor() { effect(() => { this.ownerType(); this.ownerId(); this.load(); }); }

  tone(date: string): string { const d = daysUntil(date) ?? 0; return d < 0 ? 'red' : d <= 30 ? 'orange' : 'default'; }

  load(): void {
    this.http.get<AttachmentItem[]>(`${this.base}/attachments`, { params: { ownerType: this.ownerType(), ownerId: this.ownerId() } }).subscribe((r) => this.items.set(r));
  }

  upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) { this.message.warning('Choose a file first'); return; }
    const form = new FormData();
    form.append('ownerType', this.ownerType()); form.append('ownerId', this.ownerId()); form.append('file', file);
    if (this.category.trim()) form.append('category', this.category.trim());
    if (this.expires) form.append('expiresOn', toIsoDate(this.expires));
    this.busy.set(true);
    this.http.post(`${this.base}/attachments`, form).subscribe({
      next: () => { this.busy.set(false); input.value = ''; this.category = ''; this.expires = null; this.message.success('Document uploaded'); this.load(); },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Upload failed'); },
    });
  }

  download(a: { id: string; fileName: string }): void { downloadAttachment(this.http, this.base, a); }

  remove(a: AttachmentItem): void { this.http.delete(`${this.base}/attachments/${a.id}`).subscribe(() => { this.message.success('Document removed'); this.load(); }); }
}

/** Fetches a file with the auth header and hands it to the browser as a download. */
export function downloadAttachment(http: HttpClient, base: string, a: { id: string; fileName: string }): void {
  http.get(`${base}/attachments/${a.id}/download`, { responseType: 'blob' }).subscribe((blob) => {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = a.fileName; link.click();
    URL.revokeObjectURL(url);
  });
}
