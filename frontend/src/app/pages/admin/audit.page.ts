import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AuditApi, AuditEvent } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, NzDrawerModule, NzTagModule],
  providers: [DatePipe],
  template: `
    <h1 class="erp-page-title">Audit log</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search record type or id" emptyText="No audit events" (rowClick)="selected.set($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'action') {
          <nz-tag [nzColor]="row.action === 'Insert' || row.action === 'Create' ? 'green' : (row.action === 'Delete' ? 'red' : 'blue')">{{ row.action }}</nz-tag>
        } @else if (column.key === 'recordType') {
          <nz-tag>{{ row.recordType }}</nz-tag>
        } @else if (column.key === 'recordId') {
          <span class="code-pill">{{ row.recordId }}</span>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>
    <nz-drawer [nzVisible]="!!selected()" nzTitle="Audit event" nzWidth="640" (nzOnClose)="selected.set(null)">
      <ng-container *nzDrawerContent>
        @if (selected(); as e) {
          <div style="background:#f8fafc; padding:12px; border-radius:8px; border:1px solid #e2e8f0; margin-bottom:16px;">
            <p style="margin:0 0 6px"><b>Action:</b> <nz-tag [nzColor]="e.action === 'Insert' || e.action === 'Create' ? 'green' : (e.action === 'Delete' ? 'red' : 'blue')">{{ e.action }}</nz-tag> · <b>Type:</b> {{ e.recordType }}</p>
            <p style="margin:0 0 6px"><b>Record ID:</b> <span class="code-pill">{{ e.recordId }}</span></p>
            <p style="margin:0; font-size:12px; color:#64748b;">{{ e.occurredAt }} · IP: {{ e.ipAddress }}</p>
          </div>
          <h4 style="font-weight:600; color:#1e293b;">Before</h4>
          <pre data-testid="audit-before" style="background:#f1f5f9; padding:12px; border-radius:8px; font-size:12px; max-height:200px; overflow:auto;">{{ pretty(e.before) }}</pre>
          <h4 style="font-weight:600; color:#1e293b; margin-top:16px;">After</h4>
          <pre data-testid="audit-after" style="background:#f1f5f9; padding:12px; border-radius:8px; font-size:12px; max-height:200px; overflow:auto;">{{ pretty(e.after) }}</pre>
        }
      </ng-container>
    </nz-drawer>`,
})
export class AuditPage {
  private readonly api = inject(AuditApi);
  private readonly date = inject(DatePipe);
  readonly selected = signal<AuditEvent | null>(null);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-occurredAt' });
  readonly columns: TableColumn<AuditEvent>[] = [
    { key: 'occurredAt', label: 'When', sortField: 'occurredAt', width: '180px', value: (e) => this.date.transform(e.occurredAt, 'yyyy-MM-dd HH:mm:ss') },
    { key: 'action', label: 'Action', width: '110px' },
    { key: 'recordType', label: 'Record type' },
    { key: 'recordId', label: 'Record' },
    { key: 'changedFields', label: 'Changed fields' },
    { key: 'ipAddress', label: 'IP address', width: '140px' },
  ];

  pretty(json?: string): string {
    if (!json) return '—';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }
}
