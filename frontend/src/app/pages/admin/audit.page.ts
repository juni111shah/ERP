import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { AuditApi, AuditEvent } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, NzDrawerModule],
  providers: [DatePipe],
  template: `
    <h1 class="erp-page-title">Audit log</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search record type or id" emptyText="No audit events" (rowClick)="selected.set($event)" />
    <nz-drawer [nzVisible]="!!selected()" nzTitle="Audit event" nzWidth="640" (nzOnClose)="selected.set(null)">
      <ng-container *nzDrawerContent>
        @if (selected(); as e) {
          <p><b>{{ e.action }}</b> · {{ e.recordType }} · {{ e.recordId }}</p>
          <p>{{ e.occurredAt }} · {{ e.ipAddress }}</p>
          <h4>Before</h4><pre data-testid="audit-before">{{ pretty(e.before) }}</pre>
          <h4>After</h4><pre data-testid="audit-after">{{ pretty(e.after) }}</pre>
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
