import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../../core/api/api-config';
import { CrudApiService } from '../../core/api/crud-api.service';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { daysUntil, downloadAttachment } from '../../shared/attachments.component';
import { Injectable } from '@angular/core';

import { NzIconModule } from 'ng-zorro-antd/icon';

export interface VaultRow { id: string; ownerType: string; ownerId: string; fileName: string; category?: string; description?: string; expiresOn?: string; createdAt: string; }
@Injectable({ providedIn: 'root' })
export class VaultApi extends CrudApiService<VaultRow, never> { protected readonly resource = 'attachments/vault'; }

/** Every tracked document in the workspace, soonest expiry first, with expired and expiring ones highlighted. */
@Component({
  selector: 'erp-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, PagedTableComponent, NzSelectModule, NzTagModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Document vault</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search file, category or description" emptyText="No documents yet. Upload them from a lease, property or party." (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'fileName') {
          <div style="display:flex; align-items:center; gap:8px;">
            <span nz-icon nzType="file-text" style="color:#10955d; font-size:16px;"></span>
            <span style="font-weight:600; color:#1e293b;">{{ row.fileName }}</span>
          </div>
        } @else if (column.key === 'category' && row.category) {
          <nz-tag>{{ row.category }}</nz-tag>
        } @else if (column.key === 'ownerType') {
          <nz-tag nzColor="blue">{{ row.ownerType }}</nz-tag>
        } @else if (column.key === 'expiresOn') {
          @if (row.expiresOn) {
            <nz-tag [nzColor]="tone(row.expiresOn)">{{ row.expiresOn }}</nz-tag>
          } @else {
            <span style="color:#94a3b8;">No expiry</span>
          }
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>`,
})
export class DocumentsPage {
  private readonly api = inject(VaultApi);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'expiresOn' });
  readonly columns: TableColumn<VaultRow>[] = [
    { key: 'fileName', label: 'File', sortField: 'fileName' },
    { key: 'category', label: 'Category' },
    { key: 'ownerType', label: 'Belongs to', width: '130px' },
    { key: 'expiresOn', label: 'Expires', sortField: 'expiresOn', width: '140px' },
    { key: 'description', label: 'Notes' },
  ];
  tone(date: string): string { const d = daysUntil(date) ?? 0; return d < 0 ? 'red' : d <= 30 ? 'orange' : 'default'; }
  open(row: VaultRow): void { downloadAttachment(this.http, this.base, row); }
}
