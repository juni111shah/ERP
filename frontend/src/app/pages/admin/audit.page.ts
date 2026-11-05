import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AdminApi, AuditApi, AuditEvent, UserSummary } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpLabelPipe, ErpDatePipe } from '../../shared/display.pipes';
import { displayDate } from '../../core/format/display';

@Component({
  selector: 'erp-audit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ErpDatePipe, PagedTableComponent, NzDrawerModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Audit log</h1>
    <erp-paged-table [list]="list" [columns]="columns()" searchPlaceholder="Search record type or id" emptyText="No audit events" (rowClick)="selected.set($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'action') {
          <nz-tag [nzColor]="actionColor(row.action)">{{ row.action | erpLabel }}</nz-tag>
        } @else if (column.key === 'recordType') {
          <nz-tag>{{ row.recordType | erpLabel }}</nz-tag>
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
          <div style="background:var(--erp-color-bg-subtle); padding:12px; border-radius:8px; border:1px solid var(--erp-color-border-subtle); margin-bottom:16px;">
            <p style="margin:0 0 6px"><b>Action:</b> <nz-tag [nzColor]="actionColor(e.action)">{{ e.action | erpLabel }}</nz-tag> · <b>Type:</b> {{ e.recordType | erpLabel }}</p>
            <p style="margin:0 0 6px"><b>Record ID:</b> <span class="code-pill">{{ e.recordId }}</span></p>
            <p style="margin:0; font-size:12px; color:var(--erp-color-text-muted);">{{ e.occurredAt | erpDate }} · {{ userName(e.userId) }}@if (e.ipAddress) { · IP: {{ e.ipAddress }} }</p>
          </div>
          <h4 style="font-weight:600;">Before</h4>
          <pre data-testid="audit-before" style="background:var(--erp-color-bg-subtle); padding:12px; border-radius:8px; font-size:12px; max-height:200px; overflow:auto;">{{ pretty(e.before) }}</pre>
          <h4 style="font-weight:600; margin-top:16px;">After</h4>
          <pre data-testid="audit-after" style="background:var(--erp-color-bg-subtle); padding:12px; border-radius:8px; font-size:12px; max-height:200px; overflow:auto;">{{ pretty(e.after) }}</pre>
        }
      </ng-container>
    </nz-drawer>`,
})
export class AuditPage {
  private readonly api = inject(AuditApi);
  readonly selected = signal<AuditEvent | null>(null);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-occurredAt' });
  private readonly users = toSignal(inject(AdminApi).users().pipe(catchError(() => of([] as UserSummary[]))), { initialValue: [] as UserSummary[] });
  /** Who did it, by name once the user list has loaded. An event with no user was the system's own. */
  userName(id?: string | null): string {
    if (!id) return 'System';
    return this.users().find((u) => u.id === id)?.fullName ?? 'Unknown user';
  }
  readonly columns = computed((): TableColumn<AuditEvent>[] => [
    { key: 'occurredAt', label: 'When', sortField: 'occurredAt', width: '180px', value: (e) => displayDate(e.occurredAt) },
    { key: 'userId', label: 'By', width: '170px', value: (e) => this.userName(e.userId) },
    { key: 'action', label: 'Action', width: '110px' },
    { key: 'recordType', label: 'Record type' },
    { key: 'recordId', label: 'Record' },
    { key: 'changedFields', label: 'Changed fields' },
    { key: 'ipAddress', label: 'IP address', width: '140px', hidden: true },
  ]);

  /** Whether a change added, changed or removed something; the API writes the action in the past tense. */
  actionColor(action: string): string {
    const a = (action ?? '').toLowerCase();
    return a.startsWith('creat') || a.startsWith('insert') ? 'green' : a.startsWith('delet') ? 'red' : 'blue';
  }

  pretty(json?: string): string {
    if (!json) return '—';
    try { return JSON.stringify(JSON.parse(json), null, 2); } catch { return json; }
  }
}
