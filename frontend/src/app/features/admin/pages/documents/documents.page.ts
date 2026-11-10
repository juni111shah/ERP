import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { VaultApi, VaultRow } from '@core/api/documents.api';
import { createPagedList } from '@core/api/paged-list';
import { daysUntil } from '@shared/components/attachments/attachments.component';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';

/** Every tracked document in the workspace, soonest expiry first, with expired and expiring ones highlighted. */
@Component({
  selector: 'erp-documents',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ErpDatePipe, FormsModule, PagedTableComponent, NzSelectModule, NzTagModule, NzIconModule],
  templateUrl: './documents.page.html',
})
export class DocumentsPage {
  private readonly api = inject(VaultApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'expiresOn' });
  readonly columns: TableColumn<VaultRow>[] = [
    { key: 'fileName', label: 'File', sortField: 'fileName' },
    { key: 'category', label: 'Category' },
    { key: 'ownerType', label: 'Belongs to', width: '130px' },
    { key: 'expiresOn', label: 'Expires', sortField: 'expiresOn', width: '140px' },
    { key: 'description', label: 'Notes' },
  ];

  /** The expiry tag's colour: red once expired, orange within 30 days. */
  tone(date: string): string { const d = daysUntil(date) ?? 0; return d < 0 ? 'red' : d <= 30 ? 'orange' : 'default'; }
  open(row: VaultRow): void { this.api.download(row); }
}
