import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { createPagedList } from '@core/api/paged-list';
import { ProjectsExtraApi, ProjectsConfig } from '@core/api/projects-extra.api';
import { ProjectTimesheetsApi, TimesheetDto } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { LogTimeComponent } from '../../components/log-time/log-time.component';
import { APPROVAL_STATUSES, errorText, filterOptions, PRJ_STYLES, ProjectTagComponent } from '../../projects-ui';

/** Time entries of everyone, or of one project when projectId is given. */
@Component({
  selector: 'erp-project-timesheets',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, NzTagModule, NzButtonModule, NzIconModule, NzModalModule, PagedTableComponent, HasPermissionDirective, LogTimeComponent, ProjectTagComponent],
  template: `
    <erp-paged-table [list]="list" [columns]="columns()" searchPlaceholder="Search worker name or description" emptyText="No time logged yet." [clickable]="false" scrollX="1520px" exportFilename="timesheets">
      <button toolbar *erpHasPermission="'projects.timesheets.manage'" nz-button nzType="primary" (click)="logTime()?.open(projectId())" data-testid="timesheet-new"><span nz-icon nzType="clock-circle"></span> Log time</button>
      <a toolbar nz-button routerLink="/projects/week" data-testid="timesheet-week"><span nz-icon nzType="calendar"></span> Week view</a>
      @if (config()?.timesheetApproval) {
        <button toolbar *erpHasPermission="'projects.timesheets.manage'" nz-button (click)="submitDrafts()" data-testid="timesheet-submit-drafts"><span nz-icon nzType="send"></span> Submit my drafts</button>
      }
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('workerName') { <span class="prj-strong">{{ row.workerName }}</span> }
          @case ('projectName') { <a [routerLink]="['/projects', row.projectId]" class="prj-link">{{ row.projectName }}</a> }
          @case ('hours') { <span class="prj-strong">{{ row.hours | number: '1.2-2' }} h</span> }
          @case ('hourlyRate') { <span>{{ currency() }} {{ row.hourlyRate | number: '1.2-2' }}</span> }
          @case ('totalAmount') { <span class="prj-strong">{{ currency() }} {{ row.totalAmount | number: '1.2-2' }}</span> }
          @case ('approvalStatus') { <erp-prj-tag kind="approval" [value]="row.approvalStatus" [hint]="row.rejectReason" /> }
          @case ('status') {
            @if (row.writtenOff) { <nz-tag>Written off</nz-tag> }
            @else if (!row.isBillable) { <nz-tag>Non-billable</nz-tag> }
            @else if (row.isBilled) { <nz-tag nzColor="green">Billed</nz-tag> }
            @else { <nz-tag nzColor="orange">Unbilled</nz-tag> }
          }
          @case ('actions') {
            @if (!row.isBilled && !row.writtenOff) {
              <span *erpHasPermission="'projects.timesheets.manage'" class="acts">
                @if (editable(row)) { <button nz-button nzSize="small" (click)="logTime()?.edit(row)" [attr.data-testid]="'timesheet-edit-' + row.id">Edit</button> }
                @if (row.approvalStatus === 'draft' || row.approvalStatus === 'rejected') { <button nz-button nzSize="small" (click)="submit([row.id])" [attr.data-testid]="'timesheet-submit-' + row.id">Submit</button> }
                @if (editable(row)) { <button nz-button nzSize="small" nzDanger (click)="remove(row)">Delete</button> }
              </span>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>
    <erp-log-time (saved)="list.reload()" />
  `,
  styles: [PRJ_STYLES + '.acts { display:inline-flex; gap:4px; }'],
})
export class ProjectTimesheetsPage {
  /** Set by the project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly timesheetsApi = inject(ProjectTimesheetsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly logTime = viewChild(LogTimeComponent);
  readonly config = toSignal(this.extra.config().pipe(catchError(() => of(null as ProjectsConfig | null))), { initialValue: null });

  readonly list = createPagedList((q) => {
    const id = this.projectId();
    return this.timesheetsApi.list(id
      ? { ...q, filters: [...(q.filters ?? []), { field: 'projectId', op: 'eq', value: id }] }
      : { ...q, extra: { ...(q.extra ?? {}), live: true } });
  }, { sort: '-date' });

  readonly columns = computed<TableColumn<TimesheetDto>[]>(() => [
    { key: 'date', label: 'Date', sortField: 'date', width: '110px', filterField: 'date' },
    { key: 'workerName', label: 'Worker', sortField: 'workerName', width: '150px', filter: false },
    ...(this.projectId() ? [] : [{ key: 'projectName', label: 'Project', width: '190px', filter: false as const }]),
    { key: 'taskTitle', label: 'Task', width: '170px', filter: false },
    { key: 'description', label: 'Description', width: '220px', filter: false },
    { key: 'hours', label: 'Hours', sortField: 'hours', width: '90px', align: 'right', filter: false },
    { key: 'hourlyRate', label: 'Rate', width: '120px', align: 'right', filter: false },
    { key: 'totalAmount', label: 'Value', sortField: 'totalAmount', width: '130px', align: 'right', filter: false },
    { key: 'approvalStatus', label: 'Approval', width: '130px', filterField: 'approvalStatus', filterOptions: filterOptions(APPROVAL_STATUSES) },
    { key: 'status', label: 'Billing', width: '110px', filter: false },
    { key: 'actions', label: '', width: '190px' },
  ]);

  constructor() {
    let first = true;
    effect(() => { this.projectId(); if (first) { first = false; return; } this.list.reload(); });
  }

  /** The server refuses changes to locked entries; the buttons say the same. */
  editable(r: TimesheetDto): boolean {
    if (!this.config()?.timesheetApproval) return r.approvalStatus !== 'submitted';
    return r.approvalStatus === 'draft' || r.approvalStatus === 'rejected';
  }

  submitDrafts(): void {
    const me = this.session.me()?.user?.id;
    const ids = this.list.items().filter((r) => (r.approvalStatus === 'draft' || r.approvalStatus === 'rejected') && r.workerUserId === me).map((r) => r.id);
    if (!ids.length) { this.msg.info('You have no drafts on this page.'); return; }
    this.submit(ids);
  }

  submit(ids: string[]): void {
    this.extra.submitTime(ids).subscribe({
      next: (r) => { this.msg.success(`${r.submitted} ${r.submitted === 1 ? 'entry' : 'entries'} sent for approval`); this.list.reload(); },
      error: (e) => this.msg.error(errorText(e, 'Could not submit.')),
    });
  }

  remove(r: TimesheetDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this time entry?', nzContent: `${r.workerName}, ${r.hours} h on ${r.date}`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.timesheetsApi.delete(r.id).subscribe({ next: () => { this.msg.success(`Time entry of ${r.hours} h for ${r.workerName} deleted`); this.list.reload(); }, error: (e) => this.msg.error(errorText(e, 'Could not delete the entry.')) }),
    });
  }
}
