import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuditApi, AuditEvent } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent],
  providers: [DatePipe],
  template: `
    <h1 class="erp-page-title">Audit log</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search record type or id" emptyText="No audit events" />`,
})
export class AuditPage {
  private readonly api = inject(AuditApi);
  private readonly date = inject(DatePipe);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-occurredAt' });
  readonly columns: TableColumn<AuditEvent>[] = [
    { key: 'occurredAt', label: 'When', sortField: 'occurredAt', width: '180px', value: (e) => this.date.transform(e.occurredAt, 'yyyy-MM-dd HH:mm:ss') },
    { key: 'action', label: 'Action', width: '110px' },
    { key: 'recordType', label: 'Record type' },
    { key: 'recordId', label: 'Record' },
    { key: 'changedFields', label: 'Changed fields' },
    { key: 'ipAddress', label: 'IP address', width: '140px' },
  ];
}
